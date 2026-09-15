namespace Firemka.Domain.Sales;

public enum SalesInvoiceStatus
{
    Draft = 1,
    Sending = 2,
    DeliveryUncertain = 3,
    Rejected = 4,
    Issued = 5,
    IssuedContentMismatch = 6,
}

public enum SubscriptionRateApplication
{
    NoChange = 1,
    Applied = 2,
    RequiresConfirmation = 3,
}

public sealed record SalesInvoicePreparation(bool IsLateApproval);

public sealed class SalesInvoice
{
    private SalesInvoice()
    {
    }

    private SalesInvoice(
        Guid id,
        Guid companyId,
        string ownerUserId,
        DateOnly serviceMonth,
        Guid subscriptionRatePeriodId,
        string description,
        decimal netAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        ServiceMonth = serviceMonth;
        ServicePeriodFrom = serviceMonth;
        ServicePeriodTo = serviceMonth.AddMonths(1).AddDays(-1);
        ScheduledIssueDate = serviceMonth;
        SubscriptionRatePeriodId = subscriptionRatePeriodId;
        Description = description;
        NetAmount = netAmount;
        VatRate = vatRate;
        RecalculateTotals();
        Currency = "PLN";
        Status = SalesInvoiceStatus.Draft;
        DraftRevision = 1;
        IdempotencyKey = $"sales-invoice:{companyId:N}:{serviceMonth:yyyyMM}";
        ConcurrencyStamp = Guid.NewGuid();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateOnly ServiceMonth { get; private set; }
    public DateOnly ServicePeriodFrom { get; private set; }
    public DateOnly ServicePeriodTo { get; private set; }
    public DateOnly ScheduledIssueDate { get; private set; }
    public Guid SubscriptionRatePeriodId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal NetAmount { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public string Currency { get; private set; } = "PLN";
    public bool HasManualAmountOverride { get; private set; }
    public Guid? PendingSubscriptionRatePeriodId { get; private set; }
    public decimal? PendingSubscriptionNetAmount { get; private set; }
    public decimal? PendingSubscriptionVatRate { get; private set; }
    public int DraftRevision { get; private set; }
    public SalesInvoiceStatus Status { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? PaymentDueDate { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public bool IssuedAutomatically { get; private set; }
    public string? OutgoingXml { get; private set; }
    public string? SessionReferenceNumber { get; private set; }
    public string? SubmissionReferenceNumber { get; private set; }
    public string? KsefNumber { get; private set; }
    public string? UpoXml { get; private set; }
    public string? ReturnedKsefXml { get; private set; }
    public string? RejectionReason { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid ConcurrencyStamp { get; private set; }
    public DateTimeOffset? LastStatusCheckedAtUtc { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SalesInvoice CreateDraft(
        Guid companyId,
        string ownerUserId,
        DateOnly serviceMonth,
        Guid subscriptionRatePeriodId,
        string description,
        decimal netAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        if (subscriptionRatePeriodId == Guid.Empty)
        {
            throw new ArgumentException("Okres stawki abonamentu jest wymagany.", nameof(subscriptionRatePeriodId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ValidateAmounts(netAmount, vatRate);

        return new SalesInvoice(
            Guid.NewGuid(),
            companyId,
            ownerUserId.Trim(),
            FirstDayOfMonth(serviceMonth),
            subscriptionRatePeriodId,
            description.Trim(),
            netAmount,
            vatRate,
            createdAtUtc);
    }

    public void EditDraft(decimal netAmount, decimal vatRate, DateTimeOffset updatedAtUtc)
    {
        EnsureDraft();
        ValidateAmounts(netAmount, vatRate);
        NetAmount = netAmount;
        VatRate = vatRate;
        RecalculateTotals();
        HasManualAmountOverride = true;
        ClearPendingSubscriptionRate();
        DraftRevision++;
        RotateConcurrencyStamp();
        UpdatedAtUtc = updatedAtUtc;
    }

    public SubscriptionRateApplication ApplySubscriptionRate(
        Guid subscriptionRatePeriodId,
        decimal netAmount,
        decimal vatRate,
        bool replaceManualOverride,
        DateTimeOffset updatedAtUtc)
    {
        EnsureDraft();
        if (subscriptionRatePeriodId == Guid.Empty)
        {
            throw new ArgumentException("Okres stawki abonamentu jest wymagany.", nameof(subscriptionRatePeriodId));
        }

        ValidateAmounts(netAmount, vatRate);
        if (SubscriptionRatePeriodId == subscriptionRatePeriodId
            && NetAmount == netAmount
            && VatRate == vatRate)
        {
            ClearPendingSubscriptionRate();
            return SubscriptionRateApplication.NoChange;
        }

        if (HasManualAmountOverride && !replaceManualOverride)
        {
            PendingSubscriptionRatePeriodId = subscriptionRatePeriodId;
            PendingSubscriptionNetAmount = netAmount;
            PendingSubscriptionVatRate = vatRate;
            RotateConcurrencyStamp();
            UpdatedAtUtc = updatedAtUtc;
            return SubscriptionRateApplication.RequiresConfirmation;
        }

        SubscriptionRatePeriodId = subscriptionRatePeriodId;
        NetAmount = netAmount;
        VatRate = vatRate;
        RecalculateTotals();
        HasManualAmountOverride = false;
        ClearPendingSubscriptionRate();
        DraftRevision++;
        RotateConcurrencyStamp();
        UpdatedAtUtc = updatedAtUtc;
        return SubscriptionRateApplication.Applied;
    }

    public void KeepManualOverride(DateTimeOffset updatedAtUtc)
    {
        EnsureDraft();
        if (!HasManualAmountOverride || PendingSubscriptionRatePeriodId is null)
        {
            throw new InvalidOperationException("Brak oczekującej zmiany stawki do odrzucenia.");
        }

        ClearPendingSubscriptionRate();
        RotateConcurrencyStamp();
        UpdatedAtUtc = updatedAtUtc;
    }

    public SalesInvoicePreparation PrepareForIssue(
        DateOnly actualIssueDate,
        string invoiceNumber,
        bool automatic,
        string outgoingXml,
        DateTimeOffset preparedAtUtc)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(outgoingXml);

        IssueDate = actualIssueDate;
        PaymentDueDate = actualIssueDate.AddDays(7);
        InvoiceNumber = invoiceNumber.Trim();
        IssuedAutomatically = automatic;
        OutgoingXml = outgoingXml;
        Status = SalesInvoiceStatus.Sending;
        RejectionReason = null;
        RotateConcurrencyStamp();
        UpdatedAtUtc = preparedAtUtc;

        return new SalesInvoicePreparation(actualIssueDate > ServicePeriodTo);
    }

    public void RegisterSubmission(
        string sessionReferenceNumber,
        string submissionReferenceNumber,
        DateTimeOffset submittedAtUtc)
    {
        EnsureAwaitingKsefResult();
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionReferenceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(submissionReferenceNumber);
        SessionReferenceNumber = sessionReferenceNumber.Trim();
        SubmissionReferenceNumber = submissionReferenceNumber.Trim();
        Status = SalesInvoiceStatus.Sending;
        RotateConcurrencyStamp();
        UpdatedAtUtc = submittedAtUtc;
    }

    public void MarkDeliveryUncertain(DateTimeOffset occurredAtUtc)
    {
        EnsureAwaitingKsefResult();
        Status = SalesInvoiceStatus.DeliveryUncertain;
        RotateConcurrencyStamp();
        UpdatedAtUtc = occurredAtUtc;
    }

    public void RecordStatusCheck(DateTimeOffset checkedAtUtc)
    {
        EnsureAwaitingKsefResult();
        LastStatusCheckedAtUtc = checkedAtUtc;
        RotateConcurrencyStamp();
        UpdatedAtUtc = checkedAtUtc;
    }

    public void MarkRejected(string reason, DateTimeOffset rejectedAtUtc)
    {
        EnsureAwaitingKsefResult();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RejectionReason = reason.Trim();
        Status = SalesInvoiceStatus.Rejected;
        LastStatusCheckedAtUtc = rejectedAtUtc;
        RotateConcurrencyStamp();
        UpdatedAtUtc = rejectedAtUtc;
    }

    public void ReopenRejectedForCorrection(DateTimeOffset reopenedAtUtc)
    {
        if (Status != SalesInvoiceStatus.Rejected)
        {
            throw new InvalidOperationException("Poprawić można wyłącznie fakturę odrzuconą przez KSeF.");
        }

        Status = SalesInvoiceStatus.Draft;
        IssueDate = null;
        PaymentDueDate = null;
        InvoiceNumber = null;
        IssuedAutomatically = false;
        OutgoingXml = null;
        SessionReferenceNumber = null;
        SubmissionReferenceNumber = null;
        KsefNumber = null;
        UpoXml = null;
        ReturnedKsefXml = null;
        RejectionReason = null;
        LastStatusCheckedAtUtc = null;
        IssuedAtUtc = null;
        DraftRevision++;
        IdempotencyKey = $"sales-invoice:{CompanyId:N}:{ServiceMonth:yyyyMM}:attempt:{DraftRevision}";
        RotateConcurrencyStamp();
        UpdatedAtUtc = reopenedAtUtc;
    }

    public void MarkIssued(
        string ksefNumber,
        string upoXml,
        string returnedKsefXml,
        DateTimeOffset issuedAtUtc)
    {
        EnsureAwaitingKsefResult();
        ArgumentException.ThrowIfNullOrWhiteSpace(ksefNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(upoXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnedKsefXml);
        KsefNumber = ksefNumber.Trim();
        UpoXml = upoXml;
        ReturnedKsefXml = returnedKsefXml;
        Status = SalesInvoiceStatus.Issued;
        LastStatusCheckedAtUtc = issuedAtUtc;
        IssuedAtUtc = issuedAtUtc;
        RotateConcurrencyStamp();
        UpdatedAtUtc = issuedAtUtc;
    }

    public void MarkIssuedContentMismatch(
        string ksefNumber,
        string upoXml,
        string returnedKsefXml,
        DateTimeOffset issuedAtUtc)
    {
        MarkIssued(ksefNumber, upoXml, returnedKsefXml, issuedAtUtc);
        Status = SalesInvoiceStatus.IssuedContentMismatch;
        RotateConcurrencyStamp();
    }

    private void EnsureDraft()
    {
        if (Status != SalesInvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Zmieniać można wyłącznie wersję roboczą faktury.");
        }
    }

    private void EnsureAwaitingKsefResult()
    {
        if (Status is not (SalesInvoiceStatus.Sending or SalesInvoiceStatus.DeliveryUncertain))
        {
            throw new InvalidOperationException("Faktura nie oczekuje na wynik wysyłki KSeF.");
        }
    }

    private void RecalculateTotals()
    {
        VatAmount = decimal.Round(NetAmount * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
        GrossAmount = NetAmount + VatAmount;
    }

    private void ClearPendingSubscriptionRate()
    {
        PendingSubscriptionRatePeriodId = null;
        PendingSubscriptionNetAmount = null;
        PendingSubscriptionVatRate = null;
    }

    private void RotateConcurrencyStamp() => ConcurrencyStamp = Guid.NewGuid();

    private static void ValidateAmounts(decimal netAmount, decimal vatRate)
    {
        if (netAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(netAmount));
        }

        if (vatRate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        }
    }

    private static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
