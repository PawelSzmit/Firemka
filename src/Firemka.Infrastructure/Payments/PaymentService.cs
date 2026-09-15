using System.Data;
using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.Payments;
using Firemka.Domain.Calculations;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Firemka.Infrastructure.Payments;

public sealed class PaymentService(AppDbContext dbContext, IAuditTrail auditTrail) : IPaymentService
{
    public async Task<PaymentWorkspace> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default)
    {
        var owner = Validate(ownerUserId, companyId);
        var firstDay = new DateOnly(month.Year, month.Month, 1);
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == companyId && item.OwnerUserId == owner,
            cancellationToken) ?? throw new KeyNotFoundException("Nie znaleziono firmy.");
        var all = await BuildObligationsAsync(owner, companyId, cancellationToken);
        var history = await dbContext.Payments.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .OrderByDescending(item => item.PaidOn)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return new PaymentWorkspace(
            company.Id,
            company.Name,
            firstDay,
            all.Where(item => item.Period == firstDay).ToArray(),
            history.Select(ToSnapshot).ToArray());
    }

    public async Task<IReadOnlyList<PaymentObligationSnapshot>> GetOpenAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var owner = Validate(ownerUserId, companyId);
        var companyExists = await dbContext.Companies.AsNoTracking().AnyAsync(
            item => item.Id == companyId && item.OwnerUserId == owner,
            cancellationToken);
        if (!companyExists) throw new KeyNotFoundException("Nie znaleziono firmy.");
        return (await BuildObligationsAsync(owner, companyId, cancellationToken))
            .Where(item => !item.IsPaid)
            .OrderBy(item => item.DueOn)
            .ThenBy(item => item.Kind)
            .ToArray();
    }

    public async Task<PaymentSnapshot> RecordFullAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        RecordFullPaymentCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var owner = Validate(ownerUserId, companyId);
        var firstDay = new DateOnly(month.Year, month.Month, 1);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        try
        {
            var obligation = (await BuildObligationsAsync(owner, companyId, cancellationToken))
                .SingleOrDefault(item => item.Period == firstDay
                    && item.Kind == command.Kind
                    && item.TargetId == command.TargetId
                    && item.TargetVersion == command.TargetVersion)
                ?? throw new KeyNotFoundException("Nie znaleziono tej wersji należności.");
            if (obligation.Payment is not null)
            {
                if (Matches(obligation.Payment, command))
                {
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return obligation.Payment;
                }
                throw new InvalidOperationException("Ta należność ma już zapisaną inną pełną płatność.");
            }

            var payment = Payment.Record(
                companyId,
                owner,
                obligation.Kind,
                obligation.TargetId,
                obligation.TargetVersion,
                obligation.AmountDue,
                command.AmountPaid,
                command.PaidOn,
                PaymentSource.Manual,
                command.ExternalIdentifier,
                nowUtc);
            dbContext.Payments.Add(payment);
            auditTrail.Stage(new AuditRecord(
                "full-payment-recorded",
                nameof(Payment),
                payment.Id.ToString(),
                owner,
                nowUtc,
                JsonSerializer.Serialize(new
                {
                    payment.CompanyId,
                    payment.Kind,
                    payment.TargetId,
                    payment.TargetVersion,
                    payment.AmountPaid,
                    payment.PaidOn,
                    payment.Source,
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToSnapshot(payment);
        }
        catch (Exception exception) when (IsConcurrentWrite(exception))
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.Payments.AsNoTracking().SingleOrDefaultAsync(
                item => item.OwnerUserId == owner
                    && item.Kind == command.Kind
                    && item.TargetId == command.TargetId
                    && item.TargetVersion == command.TargetVersion,
                cancellationToken);
            if (replay is not null && Matches(ToSnapshot(replay), command)) return ToSnapshot(replay);
            throw new InvalidOperationException(
                "Płatność została równocześnie zapisana inaczej. Odśwież stronę.", exception);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<PaymentObligationSnapshot>> BuildObligationsAsync(
        string owner,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var invoices = await dbContext.SalesInvoices.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == owner
                && (item.Status == SalesInvoiceStatus.Issued
                    || item.Status == SalesInvoiceStatus.IssuedContentMismatch)
                && item.PaymentDueDate != null)
            .ToListAsync(cancellationToken);
        var settlements = await dbContext.MonthSettlements.AsNoTracking()
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == owner
                && item.Status == MonthSettlementStatus.Closed
                && item.CalculationId != null)
            .ToListAsync(cancellationToken);
        var latestSettlements = settlements.GroupBy(item => item.Month)
            .Select(group => group.OrderByDescending(item => item.VersionNumber).First())
            .ToArray();
        var calculationIds = latestSettlements.Select(item => item.CalculationId!.Value).ToArray();
        var calculations = await dbContext.MonthCalculations.AsNoTracking()
            .Where(item => calculationIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var payments = await dbContext.Payments.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == owner)
            .ToListAsync(cancellationToken);
        var paymentByTarget = payments.ToDictionary(
            item => (item.Kind, item.TargetId, item.TargetVersion),
            ToSnapshot);
        var obligations = new List<PaymentObligationSnapshot>();

        foreach (var invoice in invoices)
        {
            Add(
                PaymentKind.ClientInvoice,
                invoice.Id,
                invoice.DraftRevision,
                invoice.ServiceMonth,
                $"Faktura klienta {invoice.InvoiceNumber}",
                invoice.GrossAmount,
                invoice.PaymentDueDate!.Value);
        }

        foreach (var settlement in latestSettlements)
        {
            var calculation = calculations[settlement.CalculationId!.Value];
            Add(PaymentKind.Pit, settlement.Id, settlement.VersionNumber, settlement.Month,
                "Zaliczka PIT", calculation.PitAdvanceDue, PolishDueDate.PitOrZusFor(settlement.Month));
            Add(PaymentKind.Vat, settlement.Id, settlement.VersionNumber, settlement.Month,
                "VAT", calculation.VatPayableRounded, PolishDueDate.VatFor(settlement.Month));
            Add(PaymentKind.Zus, settlement.Id, settlement.VersionNumber, settlement.Month,
                "ZUS — składka zdrowotna", calculation.HealthContribution,
                PolishDueDate.PitOrZusFor(settlement.Month));
        }

        return obligations
            .OrderBy(item => item.Period)
            .ThenBy(item => item.DueOn)
            .ThenBy(item => item.Kind)
            .ToArray();

        void Add(
            PaymentKind kind,
            Guid targetId,
            int version,
            DateOnly period,
            string label,
            decimal amount,
            DateOnly dueOn)
        {
            var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (rounded <= 0m) return;
            paymentByTarget.TryGetValue((kind, targetId, version), out var payment);
            obligations.Add(new PaymentObligationSnapshot(
                kind, targetId, version, period, label, rounded, dueOn, payment));
        }
    }

    private static bool Matches(PaymentSnapshot payment, RecordFullPaymentCommand command)
        => payment.Kind == command.Kind
            && payment.TargetId == command.TargetId
            && payment.TargetVersion == command.TargetVersion
            && payment.AmountPaid == decimal.Round(command.AmountPaid, 2, MidpointRounding.AwayFromZero)
            && payment.PaidOn == command.PaidOn
            && payment.ExternalIdentifier == command.ExternalIdentifier.Trim();

    private static PaymentSnapshot ToSnapshot(Payment payment)
        => new(
            payment.Id,
            payment.Kind,
            payment.TargetId,
            payment.TargetVersion,
            payment.AmountDue,
            payment.AmountPaid,
            payment.PaidOn,
            payment.Source,
            payment.ExternalIdentifier,
            payment.CreatedAtUtc);

    private static string Validate(string ownerUserId, Guid companyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (companyId == Guid.Empty) throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        return ownerUserId.Trim();
    }

    private static bool IsConcurrentWrite(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is PostgresException postgres
                && postgres.SqlState is PostgresErrorCodes.UniqueViolation
                    or PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.DeadlockDetected)
                return true;
        }

        return false;
    }
}
