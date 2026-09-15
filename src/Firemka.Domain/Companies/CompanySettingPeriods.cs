namespace Firemka.Domain.Companies;

public enum VatProfile
{
    ActiveMonthly = 1,
    VatExempt = 2,
}

public enum ZusProfile
{
    HealthOnlyDueToEmployment = 1,
    SocialAndHealthContributions = 2,
}

public sealed class SubscriptionRatePeriod
{
    private SubscriptionRatePeriod()
    {
    }

    internal SubscriptionRatePeriod(
        Guid id,
        Guid companyId,
        DateOnly validFromMonth,
        decimal netMonthlyAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        ValidFromMonth = validFromMonth;
        NetMonthlyAmount = netMonthlyAmount;
        VatRate = vatRate;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public DateOnly ValidFromMonth { get; private set; }
    public decimal NetMonthlyAmount { get; private set; }
    public decimal VatRate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class EnergyRatePeriod
{
    private EnergyRatePeriod()
    {
    }

    internal EnergyRatePeriod(
        Guid id,
        Guid companyId,
        DateOnly validFromMonth,
        decimal grossPricePerKwh,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        ValidFromMonth = validFromMonth;
        GrossPricePerKwh = grossPricePerKwh;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public DateOnly ValidFromMonth { get; private set; }
    public decimal GrossPricePerKwh { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class VatProfilePeriod
{
    private VatProfilePeriod()
    {
    }

    internal VatProfilePeriod(
        Guid id,
        Guid companyId,
        DateOnly validFromMonth,
        VatProfile profile,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        ValidFromMonth = validFromMonth;
        Profile = profile;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public DateOnly ValidFromMonth { get; private set; }
    public VatProfile Profile { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class ZusProfilePeriod
{
    private ZusProfilePeriod()
    {
    }

    internal ZusProfilePeriod(
        Guid id,
        Guid companyId,
        DateOnly validFromMonth,
        ZusProfile profile,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        ValidFromMonth = validFromMonth;
        Profile = profile;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public DateOnly ValidFromMonth { get; private set; }
    public ZusProfile Profile { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class VehicleProfilePeriod
{
    private VehicleProfilePeriod()
    {
    }

    internal VehicleProfilePeriod(
        Guid id,
        Guid companyId,
        DateOnly validFromMonth,
        VehicleArrangement arrangement,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        ValidFromMonth = validFromMonth;
        Arrangement = arrangement;
        MixedUse = arrangement != VehicleArrangement.None;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public DateOnly ValidFromMonth { get; private set; }
    public VehicleArrangement Arrangement { get; private set; }
    public bool MixedUse { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
