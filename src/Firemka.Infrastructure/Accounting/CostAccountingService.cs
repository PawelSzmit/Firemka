using System.Text.Json;
using Firemka.Application.Accounting;
using Firemka.Application.Auditing;
using Firemka.Domain.Accounting;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Accounting;

public sealed class CostAccountingService(
    AppDbContext dbContext,
    IAuditTrail auditTrail) : ICostAccountingService
{
    public async Task<CostBookingSnapshot> PrepareAsync(
        string ownerUserId,
        Guid sourceDocumentId,
        PrepareCostCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(command);

        var existing = await dbContext.CostBookings.AsNoTracking().SingleOrDefaultAsync(
            item => item.SourceDocumentId == sourceDocumentId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (existing is not null)
        {
            return await ToSnapshotAsync(existing, cancellationToken);
        }

        var document = await dbContext.SourceDocuments.SingleOrDefaultAsync(
            item => item.Id == sourceDocumentId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono dokumentu.");
        if (document.Status != SourceDocumentStatus.RuleToDefine)
        {
            throw new InvalidOperationException("Regułę można przygotować tylko dla dokumentu z potwierdzonymi danymi.");
        }

        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new InvalidOperationException("Firma nie została jeszcze skonfigurowana.");
        if (document.GrossAmount is null || document.IssueDate is null || string.IsNullOrWhiteSpace(document.Currency))
        {
            throw new InvalidOperationException("Dokument nie ma kompletu potwierdzonych danych.");
        }

        if (document.GrossAmount.Value != command.GrossAmount)
        {
            throw new InvalidOperationException("Kwota brutto różni się od potwierdzonego dokumentu.");
        }

        var hasSellerTaxId = !string.IsNullOrWhiteSpace(document.SellerTaxId);
        if (!hasSellerTaxId && !command.SellerNameFallbackConfirmed)
        {
            throw new InvalidOperationException(
                "Dokument nie ma identyfikatora podatkowego sprzedawcy. Jawnie potwierdź awaryjne dopasowanie po nazwie.");
        }

        var sellerKey = hasSellerTaxId
            ? $"NIP:{document.SellerTaxId}"
            : $"NAZWA:{document.SellerName}";
        var fingerprint = CostDocumentFingerprint.Create(
            sellerKey,
            command.SellerCountryCode,
            document.Currency,
            command.VatTreatment,
            command.VatRate,
            command.ServiceKind);
        var booking = CostBooking.CreatePending(
            document.Id,
            company.Id,
            ownerUserId,
            fingerprint,
            command.GrossAmount,
            command.InputVatAmount,
            document.IssueDate.Value,
            nowUtc);
        dbContext.CostBookings.Add(booking);

        var matchingRules = await dbContext.CostRules
            .Where(item => item.CompanyId == company.Id
                && item.FingerprintKey == fingerprint.ToKey()
                && item.IsActive)
            .ToListAsync(cancellationToken);
        if (matchingRules.Count > 1)
        {
            throw new InvalidOperationException("Więcej niż jedna aktywna reguła pasuje do dokumentu.");
        }

        var rule = matchingRules.SingleOrDefault();
        if (rule?.CanBookAutomatically == true)
        {
            BookAndStageLedgers(
                document,
                booking,
                rule,
                rule.KpirCategory,
                rule.VatDeductionPercent,
                rule.KpirCostPercent,
                rule.ResolveKpirPeriod(booking.IssueDate),
                rule.ResolveVatPeriod(booking.IssueDate),
                $"Automatycznie na podstawie reguły v{rule.VersionNumber}: {rule.DecisionSource}",
                automatic: true,
                nowUtc);
        }

        auditTrail.Stage(new AuditRecord(
            booking.Status == CostBookingStatus.PendingReview ? "cost-review-prepared" : "cost-booked-automatically",
            nameof(CostBooking),
            booking.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { booking.SourceDocumentId, booking.RuleId, booking.Automatic })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return await ToSnapshotAsync(booking, cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.CostBookings.AsNoTracking().SingleOrDefaultAsync(
                item => item.SourceDocumentId == sourceDocumentId && item.OwnerUserId == ownerUserId,
                cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return await ToSnapshotAsync(replay, cancellationToken);
        }
    }

    public Task ConfirmForDocumentAsync(
        string ownerUserId,
        Guid bookingId,
        CostDecisionCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => ConfirmAsync(ownerUserId, bookingId, command, createRule: false, nowUtc, cancellationToken);

    public Task ConfirmAndApplyFutureAsync(
        string ownerUserId,
        Guid bookingId,
        CostDecisionCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
        => ConfirmAsync(ownerUserId, bookingId, command, createRule: true, nowUtc, cancellationToken);

    public async Task<CostReviewSnapshot?> GetReviewAsync(
        string ownerUserId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.CostBookings.AsNoTracking().SingleOrDefaultAsync(
            item => item.SourceDocumentId == sourceDocumentId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (booking is null)
        {
            return null;
        }

        var activeRules = await dbContext.CostRules.AsNoTracking()
            .Where(item => item.CompanyId == booking.CompanyId && item.IsActive)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var closest = activeRules
            .OrderByDescending(item => Similarity(item, booking))
            .FirstOrDefault();
        return new CostReviewSnapshot(
            await ToSnapshotAsync(booking, cancellationToken),
            DescribeDifferences(closest, booking));
    }

    public async Task<IReadOnlyList<KpirEntrySnapshot>> ListKpirAsync(
        string ownerUserId,
        int year,
        CancellationToken cancellationToken = default)
        => await (from entry in dbContext.KpirEntries.AsNoTracking()
                  join booking in dbContext.CostBookings.AsNoTracking() on entry.BookingId equals booking.Id
                  join rule in dbContext.CostRules.AsNoTracking() on entry.RuleId equals rule.Id into rules
                  from rule in rules.DefaultIfEmpty()
                  where entry.OwnerUserId == ownerUserId && entry.Period.Year == year
                  orderby entry.Period, entry.CreatedAtUtc, entry.Id
                  select new KpirEntrySnapshot(
                      entry.Id,
                      entry.SourceDocumentId,
                      entry.Period,
                      entry.Category,
                      entry.Amount,
                      entry.Included,
                      booking.Automatic,
                      rule == null ? null : rule.VersionNumber))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VatEntrySnapshot>> ListVatAsync(
        string ownerUserId,
        int year,
        CancellationToken cancellationToken = default)
        => await (from entry in dbContext.VatPurchaseEntries.AsNoTracking()
                  join booking in dbContext.CostBookings.AsNoTracking() on entry.BookingId equals booking.Id
                  join rule in dbContext.CostRules.AsNoTracking() on entry.RuleId equals rule.Id into rules
                  from rule in rules.DefaultIfEmpty()
                  where entry.OwnerUserId == ownerUserId && entry.Period.Year == year
                  orderby entry.Period, entry.CreatedAtUtc, entry.Id
                  select new VatEntrySnapshot(
                      entry.Id,
                      entry.SourceDocumentId,
                      entry.Period,
                      entry.InputVatAmount,
                      entry.DeductibleVatAmount,
                      entry.Included,
                      booking.Automatic,
                      rule == null ? null : rule.VersionNumber))
            .ToListAsync(cancellationToken);

    private async Task ConfirmAsync(
        string ownerUserId,
        Guid bookingId,
        CostDecisionCommand command,
        bool createRule,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var booking = await dbContext.CostBookings.SingleOrDefaultAsync(
            item => item.Id == bookingId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono decyzji kosztowej.");
        if (booking.Status != CostBookingStatus.PendingReview)
        {
            return;
        }

        var document = await dbContext.SourceDocuments.SingleAsync(
            item => item.Id == booking.SourceDocumentId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        CostRule? rule = null;
        if (createRule)
        {
            var current = await dbContext.CostRules.SingleOrDefaultAsync(
                item => item.CompanyId == booking.CompanyId
                    && item.FingerprintKey == booking.FingerprintKey
                    && item.IsActive,
                cancellationToken);
            if (current is null)
            {
                rule = CostRule.CreateInitial(
                    booking.CompanyId,
                    ownerUserId,
                    BookingFingerprint(booking),
                    command.KpirCategory,
                    command.VatDeductionPercent,
                    command.KpirCostPercent,
                    command.KpirPeriodPolicy,
                    command.VatPeriodPolicy,
                    command.DecisionSource,
                    nowUtc);
            }
            else
            {
                current.Deactivate(nowUtc);
                rule = current.CreateRevision(
                    command.KpirCategory,
                    command.VatDeductionPercent,
                    command.KpirCostPercent,
                    command.KpirPeriodPolicy,
                    command.VatPeriodPolicy,
                    command.DecisionSource,
                    nowUtc);
            }

            dbContext.CostRules.Add(rule);
        }

        var kpirPeriod = ResolveDecisionPeriod(command.KpirPeriodPolicy, booking.IssueDate, command.KpirPeriod);
        var vatPeriod = ResolveDecisionPeriod(command.VatPeriodPolicy, booking.IssueDate, command.VatPeriod);
        BookAndStageLedgers(
            document,
            booking,
            rule,
            command.KpirCategory,
            command.VatDeductionPercent,
            command.KpirCostPercent,
            kpirPeriod,
            vatPeriod,
            command.DecisionSource,
            automatic: false,
            nowUtc);
        auditTrail.Stage(new AuditRecord(
            createRule ? "cost-booked-and-rule-saved" : "cost-booked-once",
            nameof(CostBooking),
            booking.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { booking.SourceDocumentId, RuleId = rule?.Id, createRule })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            var completed = await dbContext.CostBookings.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == bookingId && item.OwnerUserId == ownerUserId,
                cancellationToken);
            if (completed is null
                || completed.Status == CostBookingStatus.PendingReview
                || !await dbContext.KpirEntries.AsNoTracking().AnyAsync(item => item.BookingId == bookingId, cancellationToken)
                || !await dbContext.VatPurchaseEntries.AsNoTracking().AnyAsync(item => item.BookingId == bookingId, cancellationToken))
            {
                throw;
            }
        }
    }

    private void BookAndStageLedgers(
        SourceDocument document,
        CostBooking booking,
        CostRule? rule,
        string category,
        decimal vatPercent,
        decimal kpirPercent,
        DateOnly kpirPeriod,
        DateOnly vatPeriod,
        string explanation,
        bool automatic,
        DateTimeOffset nowUtc)
    {
        var calculation = booking.Book(
            rule?.Id,
            category,
            vatPercent,
            kpirPercent,
            kpirPeriod,
            vatPeriod,
            explanation,
            automatic,
            nowUtc);
        dbContext.KpirEntries.Add(KpirEntry.Create(
            booking.Id,
            booking.SourceDocumentId,
            booking.OwnerUserId,
            kpirPeriod,
            category,
            calculation.KpirAmount,
            rule?.Id,
            nowUtc));
        dbContext.VatPurchaseEntries.Add(VatPurchaseEntry.Create(
            booking.Id,
            booking.SourceDocumentId,
            booking.OwnerUserId,
            vatPeriod,
            booking.InputVatAmount,
            calculation.DeductibleVatAmount,
            rule?.Id,
            nowUtc));
        document.TransitionTo(SourceDocumentStatus.Booked, nowUtc);
    }

    private async Task<CostBookingSnapshot> ToSnapshotAsync(
        CostBooking booking,
        CancellationToken cancellationToken)
    {
        int? ruleVersion = null;
        if (booking.RuleId is Guid ruleId)
        {
            ruleVersion = await dbContext.CostRules.AsNoTracking()
                .Where(item => item.Id == ruleId)
                .Select(item => (int?)item.VersionNumber)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new CostBookingSnapshot(
            booking.Id,
            booking.SourceDocumentId,
            booking.CompanyId,
            booking.Status,
            booking.SellerKey,
            booking.SellerCountryCode,
            booking.Currency,
            booking.VatTreatment,
            booking.VatRate,
            booking.ServiceKind,
            booking.GrossAmount,
            booking.InputVatAmount,
            booking.IssueDate,
            booking.RuleId,
            ruleVersion,
            booking.DeductibleVatAmount,
            booking.KpirAmount,
            booking.Automatic,
            booking.Explanation);
    }

    private static CostDocumentFingerprint BookingFingerprint(CostBooking booking)
        => CostDocumentFingerprint.Create(
            booking.SellerKey,
            booking.SellerCountryCode,
            booking.Currency,
            booking.VatTreatment,
            booking.VatRate,
            booking.ServiceKind);

    private static DateOnly ResolveDecisionPeriod(
        AccountingPeriodPolicy policy,
        DateOnly issueDate,
        DateOnly selected)
    {
        if (policy == AccountingPeriodPolicy.Manual)
        {
            if (selected.Day != 1)
            {
                throw new ArgumentException("Ręcznie wybrany okres musi zaczynać się pierwszego dnia miesiąca.", nameof(selected));
            }

            return selected;
        }

        var issueMonth = new DateOnly(issueDate.Year, issueDate.Month, 1);
        return policy switch
        {
            AccountingPeriodPolicy.IssueMonth => issueMonth,
            AccountingPeriodPolicy.NextMonth => issueMonth.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
    }

    private static int Similarity(CostRule rule, CostBooking booking)
        => (rule.SellerKey == booking.SellerKey ? 1 : 0)
            + (rule.SellerCountryCode == booking.SellerCountryCode ? 1 : 0)
            + (rule.Currency == booking.Currency ? 1 : 0)
            + (rule.VatTreatment == booking.VatTreatment && rule.VatRate == booking.VatRate ? 1 : 0)
            + (rule.ServiceKind == booking.ServiceKind ? 1 : 0);

    private static IReadOnlyList<string> DescribeDifferences(CostRule? rule, CostBooking booking)
    {
        if (rule is null)
        {
            return ["Brak zatwierdzonej reguły dla tego rodzaju kosztu."];
        }

        var differences = new List<string>();
        if (rule.SellerKey != booking.SellerKey) differences.Add("Inny sprzedawca.");
        if (rule.SellerCountryCode != booking.SellerCountryCode) differences.Add("Inny kraj sprzedawcy.");
        if (rule.Currency != booking.Currency) differences.Add("Inna waluta.");
        if (rule.VatTreatment != booking.VatTreatment || rule.VatRate != booking.VatRate) differences.Add("Inny profil lub stawka VAT.");
        if (rule.ServiceKind != booking.ServiceKind) differences.Add("Inny rodzaj usługi lub kosztu.");
        if (differences.Count == 0 && !rule.CanBookAutomatically) differences.Add("Reguła wymaga ręcznego wyboru okresu.");
        return differences;
    }
}
