namespace Firemka.Domain.Vehicles;

public enum VehicleCostKind
{
    LeaseOrRental = 1,
    Operation = 2,
    Insurance = 3,
    PublicCharging = 4,
}

public enum VehiclePolicyStatus
{
    PendingEvidence = 1,
    Active = 2,
    Inactive = 3,
}

public sealed class VehicleCostPolicy
{
    private VehicleCostPolicy()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public VehicleCostKind Kind { get; private set; }
    public DateOnly EffectiveFromMonth { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousPolicyId { get; private set; }
    public decimal? VatDeductionPercent { get; private set; }
    public decimal? KpirCostPercent { get; private set; }
    public string? EvidenceReference { get; private set; }
    public VehiclePolicyStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static VehicleCostPolicy CreatePending(
        Guid companyId,
        string ownerUserId,
        VehicleCostKind kind,
        DateOnly effectiveFromMonth,
        DateTimeOffset nowUtc)
        => CreatePending(
            Guid.NewGuid(),
            companyId,
            ownerUserId,
            kind,
            effectiveFromMonth,
            versionNumber: 1,
            previousPolicyId: null,
            nowUtc);

    public void Activate(
        decimal? vatDeductionPercent,
        decimal? kpirCostPercent,
        string evidenceReference,
        DateTimeOffset nowUtc)
    {
        if (Status != VehiclePolicyStatus.PendingEvidence)
        {
            throw new InvalidOperationException("Aktywować można tylko politykę oczekującą na dane.");
        }

        if (vatDeductionPercent is null || kpirCostPercent is null)
        {
            throw new InvalidOperationException("Trzeba jawnie podać oba procenty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        ValidatePercent(vatDeductionPercent.Value, nameof(vatDeductionPercent));
        ValidatePercent(kpirCostPercent.Value, nameof(kpirCostPercent));

        VatDeductionPercent = vatDeductionPercent.Value;
        KpirCostPercent = kpirCostPercent.Value;
        EvidenceReference = evidenceReference.Trim();
        Status = VehiclePolicyStatus.Active;
        ActivatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public VehicleCostPolicy CreateRevision(DateOnly effectiveFromMonth, DateTimeOffset nowUtc)
    {
        if (effectiveFromMonth <= EffectiveFromMonth)
        {
            throw new InvalidOperationException("Nowa wersja polityki musi obowiązywać od późniejszego miesiąca.");
        }

        return CreatePending(
            Guid.NewGuid(),
            CompanyId,
            OwnerUserId,
            Kind,
            effectiveFromMonth,
            checked(VersionNumber + 1),
            Id,
            nowUtc);
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        if (Status != VehiclePolicyStatus.Active)
        {
            throw new InvalidOperationException("Wyłączyć można tylko aktywną politykę.");
        }

        Status = VehiclePolicyStatus.Inactive;
        DeactivatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static VehicleCostPolicy CreatePending(
        Guid id,
        Guid companyId,
        string ownerUserId,
        VehicleCostKind kind,
        DateOnly effectiveFromMonth,
        int versionNumber,
        Guid? previousPolicyId,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator firmy nie może być pusty.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (effectiveFromMonth.Day != 1)
        {
            throw new ArgumentException("Polityka musi obowiązywać od pierwszego dnia miesiąca.", nameof(effectiveFromMonth));
        }

        return new VehicleCostPolicy
        {
            Id = id,
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            Kind = kind,
            EffectiveFromMonth = effectiveFromMonth,
            VersionNumber = versionNumber,
            PreviousPolicyId = previousPolicyId,
            Status = VehiclePolicyStatus.PendingEvidence,
            CreatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
    }

    private static void ValidatePercent(decimal value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Procent musi mieścić się od 0 do 100.");
        }
    }
}
