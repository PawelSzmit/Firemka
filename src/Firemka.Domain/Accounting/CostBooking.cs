namespace Firemka.Domain.Accounting;

public enum CostBookingStatus
{
    PendingReview = 1,
    BookedManually = 2,
    BookedAutomatically = 3,
}

public sealed record CostBookingCalculation(decimal DeductibleVatAmount, decimal KpirAmount);

public sealed class CostBooking
{
    private CostBooking()
    {
    }

    public Guid Id { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public CostBookingStatus Status { get; private set; }
    public string FingerprintKey { get; private set; } = string.Empty;
    public string SellerKey { get; private set; } = string.Empty;
    public string SellerCountryCode { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public VatTreatment VatTreatment { get; private set; }
    public decimal? VatRate { get; private set; }
    public CostServiceKind ServiceKind { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal InputVatAmount { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public Guid? RuleId { get; private set; }
    public string? KpirCategory { get; private set; }
    public decimal? VatDeductionPercent { get; private set; }
    public decimal? KpirCostPercent { get; private set; }
    public DateOnly? KpirPeriod { get; private set; }
    public DateOnly? VatPeriod { get; private set; }
    public decimal? DeductibleVatAmount { get; private set; }
    public decimal? KpirAmount { get; private set; }
    public string? Explanation { get; private set; }
    public bool Automatic { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? BookedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static CostBooking CreatePending(
        Guid sourceDocumentId,
        Guid companyId,
        string ownerUserId,
        CostDocumentFingerprint fingerprint,
        decimal grossAmount,
        decimal inputVatAmount,
        DateOnly issueDate,
        DateTimeOffset nowUtc)
    {
        if (sourceDocumentId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator dokumentu nie może być pusty.", nameof(sourceDocumentId));
        }

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator firmy nie może być pusty.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (grossAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grossAmount), "Kwota brutto musi być większa od zera.");
        }

        if (inputVatAmount < 0 || inputVatAmount > grossAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(inputVatAmount), "VAT naliczony musi mieścić się od zera do kwoty brutto.");
        }

        return new CostBooking
        {
            Id = Guid.NewGuid(),
            SourceDocumentId = sourceDocumentId,
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            Status = CostBookingStatus.PendingReview,
            FingerprintKey = fingerprint.ToKey(),
            SellerKey = fingerprint.SellerKey,
            SellerCountryCode = fingerprint.SellerCountryCode,
            Currency = fingerprint.Currency,
            VatTreatment = fingerprint.VatTreatment,
            VatRate = fingerprint.VatRate,
            ServiceKind = fingerprint.ServiceKind,
            GrossAmount = grossAmount,
            InputVatAmount = inputVatAmount,
            IssueDate = issueDate,
            CreatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
    }

    public CostBookingCalculation Book(
        Guid? ruleId,
        string kpirCategory,
        decimal vatDeductionPercent,
        decimal kpirCostPercent,
        DateOnly kpirPeriod,
        DateOnly vatPeriod,
        string explanation,
        bool automatic,
        DateTimeOffset nowUtc)
    {
        if (Status != CostBookingStatus.PendingReview)
        {
            throw new InvalidOperationException("Dokument kosztowy został już zaksięgowany.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(kpirCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        ValidatePercent(vatDeductionPercent, nameof(vatDeductionPercent));
        ValidatePercent(kpirCostPercent, nameof(kpirCostPercent));
        ValidateMonth(kpirPeriod, nameof(kpirPeriod));
        ValidateMonth(vatPeriod, nameof(vatPeriod));
        if (automatic && ruleId is null)
        {
            throw new InvalidOperationException("Automatyczne księgowanie musi wskazywać zatwierdzoną regułę.");
        }

        var deductibleVat = decimal.Round(
            InputVatAmount * vatDeductionPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);
        var kpirAmount = decimal.Round(
            (GrossAmount - deductibleVat) * kpirCostPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        RuleId = ruleId;
        KpirCategory = kpirCategory.Trim();
        VatDeductionPercent = vatDeductionPercent;
        KpirCostPercent = kpirCostPercent;
        KpirPeriod = kpirPeriod;
        VatPeriod = vatPeriod;
        DeductibleVatAmount = deductibleVat;
        KpirAmount = kpirAmount;
        Explanation = explanation.Trim();
        Automatic = automatic;
        Status = automatic ? CostBookingStatus.BookedAutomatically : CostBookingStatus.BookedManually;
        BookedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
        return new CostBookingCalculation(deductibleVat, kpirAmount);
    }

    private static void ValidatePercent(decimal value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Procent musi mieścić się od 0 do 100.");
        }
    }

    private static void ValidateMonth(DateOnly month, string parameterName)
    {
        if (month.Day != 1)
        {
            throw new ArgumentException("Okres księgowy musi zaczynać się pierwszego dnia miesiąca.", parameterName);
        }
    }
}
