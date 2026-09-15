using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.Sales;
using Firemka.Domain.Companies;
using Firemka.Domain.Invoices;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Firemka.Infrastructure.Sales;

public sealed class SalesInvoiceService(
    AppDbContext dbContext,
    IOutgoingKsefGateway outgoingKsefGateway,
    IFa3InvoiceGenerator fa3InvoiceGenerator,
    IAuditTrail auditTrail,
    KsefOutgoingOptions options) : ISalesInvoiceService
{
    public async Task<SalesInvoiceSummary> EnsureDraftAsync(
        string ownerUserId,
        DateOnly serviceMonth,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var month = FirstDayOfMonth(serviceMonth);
        var company = await CompanyQuery(ownerUserId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Firma nie została jeszcze skonfigurowana.");
        var existing = await dbContext.SalesInvoices.SingleOrDefaultAsync(
            item => item.CompanyId == company.Id && item.ServiceMonth == month,
            cancellationToken);
        if (existing is not null)
        {
            return ToSummary(existing);
        }

        var rate = company.GetSubscriptionRate(month);
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            ownerUserId,
            month,
            rate.Id,
            company.ServiceDescription,
            rate.NetMonthlyAmount,
            rate.VatRate,
            nowUtc);
        dbContext.SalesInvoices.Add(invoice);
        var draftAuditId = auditTrail.Stage(new AuditRecord(
            "sales.draft.created",
            nameof(SalesInvoice),
            invoice.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { ServiceMonth = month })));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSummary(invoice);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(invoice).State = EntityState.Detached;
            var duplicateAudit = dbContext.AuditEvents.Local.SingleOrDefault(item => item.Id == draftAuditId);
            if (duplicateAudit is not null)
            {
                dbContext.Entry(duplicateAudit).State = EntityState.Detached;
            }

            var duplicate = await dbContext.SalesInvoices.AsNoTracking().SingleAsync(
                item => item.CompanyId == company.Id && item.ServiceMonth == month,
                cancellationToken);
            return ToSummary(duplicate);
        }
    }

    public async Task<IReadOnlyList<SalesInvoiceSummary>> ListAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
        => await dbContext.SalesInvoices
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId)
            .OrderByDescending(item => item.ServiceMonth)
            .Select(item => new SalesInvoiceSummary(
                item.Id,
                item.ServiceMonth,
                item.ServicePeriodFrom,
                item.ServicePeriodTo,
                item.NetAmount,
                item.VatAmount,
                item.GrossAmount,
                item.Currency,
                item.Status,
                item.HasManualAmountOverride,
                item.PendingSubscriptionRatePeriodId != null,
                item.IssueDate,
                item.PaymentDueDate,
                item.InvoiceNumber,
                item.KsefNumber,
                item.RejectionReason))
            .ToListAsync(cancellationToken);

    public async Task<SalesInvoiceDetails?> GetAsync(
        string ownerUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.SalesInvoices.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == invoiceId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var automation = await GetAutomationAsync(ownerUserId, cancellationToken);
        return new SalesInvoiceDetails(
            ToSummary(invoice),
            invoice.Description,
            invoice.VatRate,
            invoice.PendingSubscriptionNetAmount,
            invoice.PendingSubscriptionVatRate,
            invoice.IssueDate > invoice.ServicePeriodTo,
            options.Enabled && options.AdapterConfigured,
            automation.Enabled,
            automation.CanBeEnabled);
    }

    public async Task EditDraftAsync(
        string ownerUserId,
        Guid invoiceId,
        decimal netAmount,
        decimal vatRate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        invoice.EditDraft(netAmount, vatRate, nowUtc);
        auditTrail.Stage(new AuditRecord(
            "sales.draft.edited",
            nameof(SalesInvoice),
            invoice.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { invoice.NetAmount, invoice.VatRate, invoice.DraftRevision })));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyPendingSubscriptionRateAsync(
        string ownerUserId,
        Guid invoiceId,
        bool replaceManualOverride,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        if (invoice.PendingSubscriptionRatePeriodId is not { } rateId
            || invoice.PendingSubscriptionNetAmount is not { } netAmount
            || invoice.PendingSubscriptionVatRate is not { } vatRate)
        {
            throw new InvalidOperationException("Ta wersja robocza nie oczekuje na decyzję o nowej stawce.");
        }

        SubscriptionRateApplication result;
        if (replaceManualOverride)
        {
            result = invoice.ApplySubscriptionRate(
                rateId,
                netAmount,
                vatRate,
                replaceManualOverride: true,
                nowUtc);
        }
        else
        {
            invoice.KeepManualOverride(nowUtc);
            result = SubscriptionRateApplication.NoChange;
        }
        auditTrail.Stage(new AuditRecord(
            replaceManualOverride ? "sales.draft.rate-replaced" : "sales.draft.rate-kept",
            nameof(SalesInvoice),
            invoice.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { Result = result.ToString(), netAmount, vatRate })));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenRejectedAsync(
        string ownerUserId,
        Guid invoiceId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        if (invoice.Status == SalesInvoiceStatus.Draft
            && await dbContext.InvoiceVersions.AnyAsync(
                item => item.InvoiceId == invoice.Id,
                cancellationToken))
        {
            return;
        }

        if (invoice.Status != SalesInvoiceStatus.Rejected)
        {
            throw new InvalidOperationException("Poprawić można wyłącznie fakturę odrzuconą przez KSeF.");
        }

        await StageImmutableVersionAsync(invoice, nowUtc, cancellationToken);
        invoice.ReopenRejectedForCorrection(nowUtc);
        auditTrail.Stage(new AuditRecord(
            "sales.invoice.reopened-after-rejection",
            nameof(SalesInvoice),
            invoice.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { invoice.ServiceMonth, invoice.DraftRevision, invoice.IdempotencyKey })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await AcceptConcurrentReopenAsync(ownerUserId, invoiceId, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await AcceptConcurrentReopenAsync(ownerUserId, invoiceId, cancellationToken);
        }
    }

    public async Task<IssueSalesInvoiceResult> IssueAsync(
        string ownerUserId,
        Guid invoiceId,
        bool automatic,
        DateOnly actualIssueDate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var invoice = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        if (invoice.Status is SalesInvoiceStatus.Issued or SalesInvoiceStatus.IssuedContentMismatch)
        {
            return Result(invoice, invoice.IssueDate > invoice.ServicePeriodTo, "Faktura została już wystawiona.");
        }

        if (invoice.Status == SalesInvoiceStatus.Rejected)
        {
            return Result(invoice, invoice.IssueDate > invoice.ServicePeriodTo, invoice.RejectionReason ?? "KSeF odrzucił fakturę.");
        }

        if (invoice.Status is SalesInvoiceStatus.Sending or SalesInvoiceStatus.DeliveryUncertain)
        {
            options.ValidateForUse();
            if (invoice.Status == SalesInvoiceStatus.Sending
                && invoice.SubmissionReferenceNumber is null
                && nowUtc - invoice.UpdatedAtUtc < TimeSpan.FromMinutes(2))
            {
                return Result(
                    invoice,
                    invoice.IssueDate > invoice.ServicePeriodTo,
                    "Inny proces właśnie wysyła tę fakturę. Druga kopia nie została utworzona.");
            }

            return await CheckExistingSubmissionAsync(invoice, nowUtc, cancellationToken);
        }

        if (automatic)
        {
            options.ValidateAutomationUse();
            var automation = await GetAutomationAsync(ownerUserId, cancellationToken);
            if (!automation.Enabled)
            {
                throw new InvalidOperationException("Właściciel nie włączył automatycznej wysyłki.");
            }
        }
        else
        {
            options.ValidateForUse();
        }

        if (automatic && actualIssueDate != invoice.ScheduledIssueDate)
        {
            throw new InvalidOperationException("Automatyczna wysyłka może nastąpić wyłącznie w zaplanowanym dniu.");
        }

        if (actualIssueDate < invoice.ScheduledIssueDate)
        {
            throw new InvalidOperationException("Faktury abonamentowej nie można wystawić przed początkiem miesiąca usługi.");
        }

        var company = await CompanyQuery(ownerUserId).SingleAsync(cancellationToken);
        var invoiceNumber = $"FV/{invoice.ServiceMonth:yyyy/MM}";
        var paymentDueDate = actualIssueDate.AddDays(7);
        var input = new Fa3InvoiceInput(
            invoice.Id,
            invoiceNumber,
            nowUtc,
            actualIssueDate,
            invoice.ServicePeriodFrom,
            invoice.ServicePeriodTo,
            company.Name,
            company.Nip,
            company.Address,
            company.Counterparty.Name,
            company.Counterparty.Nip,
            company.Counterparty.Address,
            invoice.Description,
            invoice.NetAmount,
            invoice.VatRate,
            invoice.VatAmount,
            invoice.GrossAmount,
            paymentDueDate,
            invoice.Currency);
        var xml = fa3InvoiceGenerator.Generate(input);
        fa3InvoiceGenerator.Validate(xml);
        var preparation = invoice.PrepareForIssue(
            actualIssueDate,
            invoiceNumber,
            automatic,
            xml,
            nowUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(invoice).State = EntityState.Detached;
            var current = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
            return Result(
                current,
                current.IssueDate > current.ServicePeriodTo,
                "Inny proces rozpoczął już wysyłkę tej faktury. Druga kopia nie została utworzona.");
        }

        try
        {
            var submission = await outgoingKsefGateway.SendAsync(
                new OutgoingKsefInvoice(invoice.IdempotencyKey, xml),
                cancellationToken);
            invoice.RegisterSubmission(
                submission.SessionReferenceNumber,
                submission.SubmissionReferenceNumber,
                nowUtc);
            auditTrail.Stage(new AuditRecord(
                "sales.invoice.submitted",
                nameof(SalesInvoice),
                invoice.Id.ToString(),
                automatic ? "automation" : ownerUserId,
                nowUtc,
                JsonSerializer.Serialize(new
                {
                    invoice.ServiceMonth,
                    submission.SessionReferenceNumber,
                    submission.SubmissionReferenceNumber,
                })));
            await dbContext.SaveChangesAsync(cancellationToken);
            return await CheckExistingSubmissionAsync(invoice, nowUtc, cancellationToken, preparation.IsLateApproval);
        }
        catch (Exception exception) when (IsUncertainDelivery(exception, cancellationToken))
        {
            invoice.MarkDeliveryUncertain(nowUtc);
            auditTrail.Stage(new AuditRecord(
                "sales.invoice.delivery-uncertain",
                nameof(SalesInvoice),
                invoice.Id.ToString(),
                automatic ? "automation" : ownerUserId,
                nowUtc,
                JsonSerializer.Serialize(new { invoice.ServiceMonth, invoice.IdempotencyKey })));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result(
                invoice,
                preparation.IsLateApproval,
                "Nie wiadomo jeszcze, czy KSeF przyjął dokument. Przed każdą kolejną próbą aplikacja sprawdzi jego status.");
        }
    }

    private static bool IsUncertainDelivery(
        Exception exception,
        CancellationToken cancellationToken)
        => exception is OutgoingKsefDeliveryUncertainException or HttpRequestException or TimeoutException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    public async Task<SalesAutomationSnapshot> GetAutomationAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateAutomationSettingsAsync(ownerUserId, cancellationToken);
        return new SalesAutomationSnapshot(settings.Enabled, CanEnableAutomation(), settings.ChangedAtUtc);
    }

    public async Task SetAutomationAsync(
        string ownerUserId,
        bool enabled,
        bool warningAcknowledged,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (enabled)
        {
            options.ValidateAutomationUse();
        }

        var settings = await GetOrCreateAutomationSettingsAsync(ownerUserId, cancellationToken);
        if (enabled)
        {
            settings.Enable(warningAcknowledged, nowUtc);
        }
        else
        {
            settings.Disable(nowUtc);
        }

        auditTrail.Stage(new AuditRecord(
            enabled ? "sales.automation.enabled" : "sales.automation.disabled",
            nameof(SalesAutomationSettings),
            settings.Id.ToString(),
            ownerUserId,
            nowUtc,
            JsonSerializer.Serialize(new { Enabled = enabled, WarningAcknowledged = warningAcknowledged })));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IssueSalesInvoiceResult> CheckExistingSubmissionAsync(
        SalesInvoice invoice,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken,
        bool? isLateApproval = null)
    {
        OutgoingKsefStatus status;
        try
        {
            status = await outgoingKsefGateway.GetStatusAsync(
                invoice.IdempotencyKey,
                invoice.SessionReferenceNumber,
                invoice.SubmissionReferenceNumber,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            invoice.MarkDeliveryUncertain(nowUtc);
            return await SaveStatusResultAsync(
                invoice,
                isLateApproval ?? invoice.IssueDate > invoice.ServicePeriodTo,
                "Nie udało się sprawdzić statusu KSeF. Dokument nie został wysłany ponownie.",
                cancellationToken);
        }

        if (invoice.SubmissionReferenceNumber is null
            && !string.IsNullOrWhiteSpace(status.SessionReferenceNumber)
            && !string.IsNullOrWhiteSpace(status.SubmissionReferenceNumber))
        {
            invoice.RegisterSubmission(
                status.SessionReferenceNumber,
                status.SubmissionReferenceNumber,
                nowUtc);
        }

        switch (status.State)
        {
            case OutgoingKsefProcessingState.Accepted:
                var ksefNumber = status.KsefNumber ?? throw IncompleteStatus("numer KSeF");
                var upoXml = status.UpoXml ?? throw IncompleteStatus("UPO");
                var returnedInvoiceXml = status.ReturnedInvoiceXml
                    ?? throw IncompleteStatus("fakturę zwróconą przez KSeF");
                var contentMatches = string.Equals(
                    invoice.OutgoingXml,
                    returnedInvoiceXml,
                    StringComparison.Ordinal);
                if (contentMatches)
                {
                    invoice.MarkIssued(ksefNumber, upoXml, returnedInvoiceXml, nowUtc);
                }
                else
                {
                    invoice.MarkIssuedContentMismatch(ksefNumber, upoXml, returnedInvoiceXml, nowUtc);
                }

                await StageImmutableVersionAsync(invoice, nowUtc, cancellationToken);
                auditTrail.Stage(new AuditRecord(
                    contentMatches ? "sales.invoice.issued" : "sales.invoice.content-mismatch",
                    nameof(SalesInvoice),
                    invoice.Id.ToString(),
                    invoice.IssuedAutomatically ? "automation" : invoice.OwnerUserId,
                    nowUtc,
                    JsonSerializer.Serialize(new { invoice.ServiceMonth, invoice.KsefNumber })));
                return await SaveStatusResultAsync(
                    invoice,
                    isLateApproval ?? invoice.IssueDate > invoice.ServicePeriodTo,
                    contentMatches
                        ? "KSeF przyjął fakturę. Zwrócony dokument jest zgodny z wysłaną wersją."
                        : "KSeF przyjął fakturę, ale zwrócona treść różni się od wysłanej. Dokument wymaga ręcznego wyjaśnienia.",
                    cancellationToken);

            case OutgoingKsefProcessingState.Rejected:
                invoice.MarkRejected(
                    status.RejectionReason ?? "KSeF odrzucił dokument bez dodatkowego opisu.",
                    nowUtc);
                auditTrail.Stage(new AuditRecord(
                    "sales.invoice.rejected",
                    nameof(SalesInvoice),
                    invoice.Id.ToString(),
                    invoice.IssuedAutomatically ? "automation" : invoice.OwnerUserId,
                    nowUtc,
                    JsonSerializer.Serialize(new { invoice.ServiceMonth, invoice.RejectionReason })));
                return await SaveStatusResultAsync(
                    invoice,
                    isLateApproval ?? invoice.IssueDate > invoice.ServicePeriodTo,
                    invoice.RejectionReason!,
                    cancellationToken);

            case OutgoingKsefProcessingState.Pending:
                invoice.RecordStatusCheck(nowUtc);
                return await SaveStatusResultAsync(
                    invoice,
                    isLateApproval ?? invoice.IssueDate > invoice.ServicePeriodTo,
                    "KSeF nadal przetwarza fakturę. Nie wysłano drugiej kopii.",
                    cancellationToken);

            case OutgoingKsefProcessingState.NotFound:
                invoice.MarkDeliveryUncertain(nowUtc);
                return await SaveStatusResultAsync(
                    invoice,
                    isLateApproval ?? invoice.IssueDate > invoice.ServicePeriodTo,
                    "KSeF nie odnalazł jeszcze dokumentu. Aplikacja nie wyśle go ponownie bez rozstrzygnięcia statusu.",
                    cancellationToken);

            default:
                throw new InvalidOperationException("Nieznany status wysyłki KSeF.");
        }
    }

    private async Task StageImmutableVersionAsync(
        SalesInvoice invoice,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (dbContext.InvoiceVersions.Local.Any(item => item.InvoiceId == invoice.Id
            && dbContext.Entry(item).State == EntityState.Added))
        {
            return;
        }

        var snapshotJson = JsonSerializer.Serialize(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.ServicePeriodFrom,
            invoice.ServicePeriodTo,
            invoice.IssueDate,
            invoice.PaymentDueDate,
            invoice.NetAmount,
            invoice.VatAmount,
            invoice.GrossAmount,
            invoice.Currency,
            Status = invoice.Status.ToString(),
            invoice.IdempotencyKey,
            invoice.SessionReferenceNumber,
            invoice.SubmissionReferenceNumber,
            invoice.KsefNumber,
            invoice.OutgoingXml,
            invoice.ReturnedKsefXml,
            invoice.UpoXml,
            invoice.RejectionReason,
        });
        var previous = await dbContext.InvoiceVersions
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        dbContext.InvoiceVersions.Add(previous is null
            ? InvoiceVersion.CreateInitial(invoice.Id, snapshotJson, nowUtc)
            : previous.CreateCorrection(snapshotJson, nowUtc));
    }

    private async Task<IssueSalesInvoiceResult> SaveStatusResultAsync(
        SalesInvoice invoice,
        bool isLateApproval,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result(invoice, isLateApproval, message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReadConcurrentStatusResultAsync(invoice.OwnerUserId, invoice.Id, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            var concurrent = await ReadConcurrentStatusResultAsync(
                invoice.OwnerUserId,
                invoice.Id,
                cancellationToken);
            if (concurrent.Status is not (SalesInvoiceStatus.Rejected
                or SalesInvoiceStatus.Issued
                or SalesInvoiceStatus.IssuedContentMismatch))
            {
                throw;
            }

            return concurrent;
        }
    }

    private async Task<IssueSalesInvoiceResult> ReadConcurrentStatusResultAsync(
        string ownerUserId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var current = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        return Result(
            current,
            current.IssueDate > current.ServicePeriodTo,
            "Stan faktury został w międzyczasie zaktualizowany przez inny proces. Nie wysłano drugiej kopii.");
    }

    private async Task AcceptConcurrentReopenAsync(
        string ownerUserId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var current = await GetOwnedInvoiceAsync(ownerUserId, invoiceId, cancellationToken);
        if (current.Status == SalesInvoiceStatus.Draft
            && await dbContext.InvoiceVersions.AnyAsync(
                item => item.InvoiceId == invoiceId,
                cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException(
            "Stan faktury zmienił się podczas otwierania do poprawy. Odśwież stronę i spróbuj ponownie.");
    }

    private IQueryable<Company> CompanyQuery(string ownerUserId)
        => dbContext.Companies
            .Where(item => item.OwnerUserId == ownerUserId)
            .Include(item => item.Counterparty)
            .Include(item => item.SubscriptionRates)
            .AsSplitQuery();

    private async Task<SalesInvoice> GetOwnedInvoiceAsync(
        string ownerUserId,
        Guid invoiceId,
        CancellationToken cancellationToken)
        => await dbContext.SalesInvoices.SingleOrDefaultAsync(
            item => item.Id == invoiceId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new InvalidOperationException("Nie znaleziono faktury sprzedaży.");

    private async Task<SalesAutomationSettings> GetOrCreateAutomationSettingsAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.SalesAutomationSettings.SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new InvalidOperationException("Firma nie została jeszcze skonfigurowana.");
        var settings = SalesAutomationSettings.CreateDisabled(company.Id, ownerUserId, company.CreatedAtUtc);
        dbContext.SalesAutomationSettings.Add(settings);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return settings;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(settings).State = EntityState.Detached;
            return await dbContext.SalesAutomationSettings.SingleAsync(
                item => item.OwnerUserId == ownerUserId,
                cancellationToken);
        }
    }

    private bool CanEnableAutomation()
        => options.Enabled
            && options.AdapterConfigured
            && options.AllowAutomation
            && (options.Environment != Firemka.Application.ExternalServices.KsefEnvironment.Production
                || options.AllowProduction);

    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static InvalidOperationException IncompleteStatus(string missingValue)
        => new($"KSeF zwrócił status przyjęcia bez danych: {missingValue}.");

    private static IssueSalesInvoiceResult Result(
        SalesInvoice invoice,
        bool isLateApproval,
        string message)
        => new(invoice.Status, isLateApproval, message, invoice.KsefNumber);

    private static SalesInvoiceSummary ToSummary(SalesInvoice invoice)
        => new(
            invoice.Id,
            invoice.ServiceMonth,
            invoice.ServicePeriodFrom,
            invoice.ServicePeriodTo,
            invoice.NetAmount,
            invoice.VatAmount,
            invoice.GrossAmount,
            invoice.Currency,
            invoice.Status,
            invoice.HasManualAmountOverride,
            invoice.PendingSubscriptionRatePeriodId != null,
            invoice.IssueDate,
            invoice.PaymentDueDate,
            invoice.InvoiceNumber,
            invoice.KsefNumber,
            invoice.RejectionReason);

    private static DateOnly FirstDayOfMonth(DateOnly value) => new(value.Year, value.Month, 1);
}
