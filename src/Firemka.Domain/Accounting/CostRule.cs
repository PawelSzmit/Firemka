namespace Firemka.Domain.Accounting;

public sealed class CostRule
{
    private CostRule()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public string FingerprintKey { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public Guid? PreviousRuleId { get; private set; }
    public string SellerKey { get; private set; } = string.Empty;
    public string SellerCountryCode { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public VatTreatment VatTreatment { get; private set; }
    public decimal? VatRate { get; private set; }
    public CostServiceKind ServiceKind { get; private set; }
    public string KpirCategory { get; private set; } = string.Empty;
    public decimal VatDeductionPercent { get; private set; }
    public decimal KpirCostPercent { get; private set; }
    public AccountingPeriodPolicy KpirPeriodPolicy { get; private set; }
    public AccountingPeriodPolicy VatPeriodPolicy { get; private set; }
    public string DecisionSource { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    public CostDocumentFingerprint Fingerprint => CostDocumentFingerprint.Create(
        SellerKey,
        SellerCountryCode,
        Currency,
        VatTreatment,
        VatRate,
        ServiceKind);

    public bool CanBookAutomatically =>
        IsActive
        && KpirPeriodPolicy != AccountingPeriodPolicy.Manual
        && VatPeriodPolicy != AccountingPeriodPolicy.Manual;

    public static CostRule CreateInitial(
        Guid companyId,
        string ownerUserId,
        CostDocumentFingerprint fingerprint,
        string kpirCategory,
        decimal vatDeductionPercent,
        decimal kpirCostPercent,
        AccountingPeriodPolicy kpirPeriodPolicy,
        AccountingPeriodPolicy vatPeriodPolicy,
        string decisionSource,
        DateTimeOffset nowUtc)
        => Create(
            Guid.NewGuid(),
            companyId,
            ownerUserId,
            fingerprint,
            kpirCategory,
            vatDeductionPercent,
            kpirCostPercent,
            kpirPeriodPolicy,
            vatPeriodPolicy,
            decisionSource,
            1,
            null,
            nowUtc);

    public CostRule CreateRevision(
        string kpirCategory,
        decimal vatDeductionPercent,
        decimal kpirCostPercent,
        AccountingPeriodPolicy kpirPeriodPolicy,
        AccountingPeriodPolicy vatPeriodPolicy,
        string decisionSource,
        DateTimeOffset nowUtc)
        => Create(
            Guid.NewGuid(),
            CompanyId,
            OwnerUserId,
            Fingerprint,
            kpirCategory,
            vatDeductionPercent,
            kpirCostPercent,
            kpirPeriodPolicy,
            vatPeriodPolicy,
            decisionSource,
            checked(VersionNumber + 1),
            Id,
            nowUtc);

    public bool Matches(CostDocumentFingerprint fingerprint)
        => FingerprintKey == fingerprint.ToKey();

    public DateOnly ResolveKpirPeriod(DateOnly issueDate)
        => ResolvePeriod(KpirPeriodPolicy, issueDate);

    public DateOnly ResolveVatPeriod(DateOnly issueDate)
        => ResolvePeriod(VatPeriodPolicy, issueDate);

    public void Deactivate(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAtUtc = nowUtc;
    }

    private static CostRule Create(
        Guid id,
        Guid companyId,
        string ownerUserId,
        CostDocumentFingerprint fingerprint,
        string kpirCategory,
        decimal vatDeductionPercent,
        decimal kpirCostPercent,
        AccountingPeriodPolicy kpirPeriodPolicy,
        AccountingPeriodPolicy vatPeriodPolicy,
        string decisionSource,
        int versionNumber,
        Guid? previousRuleId,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator firmy nie może być pusty.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(kpirCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionSource);
        ValidatePercent(vatDeductionPercent, nameof(vatDeductionPercent));
        ValidatePercent(kpirCostPercent, nameof(kpirCostPercent));
        ValidatePolicy(kpirPeriodPolicy, nameof(kpirPeriodPolicy));
        ValidatePolicy(vatPeriodPolicy, nameof(vatPeriodPolicy));

        return new CostRule
        {
            Id = id,
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            FingerprintKey = fingerprint.ToKey(),
            VersionNumber = versionNumber,
            PreviousRuleId = previousRuleId,
            SellerKey = fingerprint.SellerKey,
            SellerCountryCode = fingerprint.SellerCountryCode,
            Currency = fingerprint.Currency,
            VatTreatment = fingerprint.VatTreatment,
            VatRate = fingerprint.VatRate,
            ServiceKind = fingerprint.ServiceKind,
            KpirCategory = kpirCategory.Trim(),
            VatDeductionPercent = vatDeductionPercent,
            KpirCostPercent = kpirCostPercent,
            KpirPeriodPolicy = kpirPeriodPolicy,
            VatPeriodPolicy = vatPeriodPolicy,
            DecisionSource = decisionSource.Trim(),
            IsActive = true,
            CreatedAtUtc = nowUtc,
        };
    }

    private static DateOnly ResolvePeriod(AccountingPeriodPolicy policy, DateOnly issueDate)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        var issueMonth = new DateOnly(issueDate.Year, issueDate.Month, 1);
        return policy switch
        {
            AccountingPeriodPolicy.IssueMonth => issueMonth,
            AccountingPeriodPolicy.NextMonth => issueMonth.AddMonths(1),
            AccountingPeriodPolicy.Manual => throw new InvalidOperationException("Okres wymaga ręcznego wyboru."),
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
    }

    private static void ValidatePercent(decimal value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Procent musi mieścić się od 0 do 100.");
        }
    }

    private static void ValidatePolicy(AccountingPeriodPolicy value, string parameterName)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
