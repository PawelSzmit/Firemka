using Firemka.Domain.TaxYears;

namespace Firemka.Domain.Companies;

public sealed class Company
{
    private readonly List<TaxYear> _taxYears = [];
    private readonly List<SubscriptionRatePeriod> _subscriptionRates = [];
    private readonly List<EnergyRatePeriod> _energyRates = [];
    private readonly List<VatProfilePeriod> _vatProfiles = [];
    private readonly List<ZusProfilePeriod> _zusProfiles = [];
    private readonly List<VehicleProfilePeriod> _vehicleProfiles = [];

    private Company()
    {
    }

    private Company(
        Guid id,
        string ownerUserId,
        string name,
        string nip,
        string address,
        string serviceDescription,
        DateOnly businessStartDate,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Name = name;
        Nip = nip;
        Address = address;
        ServiceDescription = serviceDescription;
        BusinessStartDate = businessStartDate;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Nip { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string ServiceDescription { get; private set; } = string.Empty;
    public DateOnly BusinessStartDate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Counterparty Counterparty { get; private set; } = null!;
    public IReadOnlyCollection<TaxYear> TaxYears => _taxYears.AsReadOnly();
    public IReadOnlyCollection<SubscriptionRatePeriod> SubscriptionRates => _subscriptionRates.AsReadOnly();
    public IReadOnlyCollection<EnergyRatePeriod> EnergyRates => _energyRates.AsReadOnly();
    public IReadOnlyCollection<VatProfilePeriod> VatProfiles => _vatProfiles.AsReadOnly();
    public IReadOnlyCollection<ZusProfilePeriod> ZusProfiles => _zusProfiles.AsReadOnly();
    public IReadOnlyCollection<VehicleProfilePeriod> VehicleProfiles => _vehicleProfiles.AsReadOnly();

    public static Company Register(
        string ownerUserId,
        string name,
        string nip,
        string address,
        DateOnly businessStartDate,
        string counterpartyName,
        string counterpartyNip,
        string counterpartyAddress,
        string serviceDescription,
        decimal subscriptionNetMonthlyAmount,
        decimal subscriptionVatRate,
        decimal energyGrossPricePerKwh,
        VehicleArrangement vehicleArrangement,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(nip);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterpartyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterpartyNip);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterpartyAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceDescription);
        ValidateMonthValue(subscriptionNetMonthlyAmount, nameof(subscriptionNetMonthlyAmount));
        ValidateVatRate(subscriptionVatRate);
        ValidateMonthValue(energyGrossPricePerKwh, nameof(energyGrossPricePerKwh));
        ValidateVehicleArrangement(vehicleArrangement);

        var companyId = Guid.NewGuid();
        var company = new Company(
            companyId,
            ownerUserId.Trim(),
            name.Trim(),
            nip.Trim(),
            address.Trim(),
            serviceDescription.Trim(),
            businessStartDate,
            createdAtUtc)
        {
            Counterparty = new Counterparty(
                Guid.NewGuid(),
                companyId,
                counterpartyName.Trim(),
                counterpartyNip.Trim(),
                counterpartyAddress.Trim()),
        };

        var firstMonth = FirstDayOfMonth(businessStartDate);
        company._taxYears.Add(TaxYear.Open(companyId, businessStartDate.Year, TaxationForm.TaxScale, createdAtUtc));
        company._subscriptionRates.Add(new SubscriptionRatePeriod(
            Guid.NewGuid(), companyId, firstMonth, subscriptionNetMonthlyAmount, subscriptionVatRate, createdAtUtc));
        company._energyRates.Add(new EnergyRatePeriod(
            Guid.NewGuid(), companyId, firstMonth, energyGrossPricePerKwh, createdAtUtc));
        company._vatProfiles.Add(new VatProfilePeriod(
            Guid.NewGuid(), companyId, firstMonth, VatProfile.ActiveMonthly, createdAtUtc));
        company._zusProfiles.Add(new ZusProfilePeriod(
            Guid.NewGuid(), companyId, firstMonth, ZusProfile.HealthOnlyDueToEmployment, createdAtUtc));
        company._vehicleProfiles.Add(new VehicleProfilePeriod(
            Guid.NewGuid(), companyId, firstMonth, vehicleArrangement, createdAtUtc));
        return company;
    }

    public SubscriptionRatePeriod GetSubscriptionRate(DateOnly month)
        => ResolvePeriod(_subscriptionRates, month, item => item.ValidFromMonth);

    public EnergyRatePeriod GetEnergyRate(DateOnly month)
        => ResolvePeriod(_energyRates, month, item => item.ValidFromMonth);

    public VatProfilePeriod GetVatProfile(DateOnly month)
        => ResolvePeriod(_vatProfiles, month, item => item.ValidFromMonth);

    public ZusProfilePeriod GetZusProfile(DateOnly month)
        => ResolvePeriod(_zusProfiles, month, item => item.ValidFromMonth);

    public VehicleProfilePeriod GetVehicleProfile(DateOnly month)
        => ResolvePeriod(_vehicleProfiles, month, item => item.ValidFromMonth);

    public void ChangeSubscriptionRate(
        DateOnly validFromMonth,
        decimal netMonthlyAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewPeriod(_subscriptionRates.Select(item => item.ValidFromMonth), validFromMonth);
        ValidateMonthValue(netMonthlyAmount, nameof(netMonthlyAmount));
        ValidateVatRate(vatRate);
        _subscriptionRates.Add(new SubscriptionRatePeriod(
            Guid.NewGuid(), Id, validFromMonth, netMonthlyAmount, vatRate, createdAtUtc));
    }

    public void ChangeEnergyRate(
        DateOnly validFromMonth,
        decimal grossPricePerKwh,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewPeriod(_energyRates.Select(item => item.ValidFromMonth), validFromMonth);
        ValidateMonthValue(grossPricePerKwh, nameof(grossPricePerKwh));
        _energyRates.Add(new EnergyRatePeriod(
            Guid.NewGuid(), Id, validFromMonth, grossPricePerKwh, createdAtUtc));
    }

    public void ChangeVatProfile(
        DateOnly validFromMonth,
        VatProfile profile,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewPeriod(_vatProfiles.Select(item => item.ValidFromMonth), validFromMonth);
        ValidateEnum(profile, nameof(profile));
        _vatProfiles.Add(new VatProfilePeriod(Guid.NewGuid(), Id, validFromMonth, profile, createdAtUtc));
    }

    public void ChangeZusProfile(
        DateOnly validFromMonth,
        ZusProfile profile,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewPeriod(_zusProfiles.Select(item => item.ValidFromMonth), validFromMonth);
        ValidateEnum(profile, nameof(profile));
        _zusProfiles.Add(new ZusProfilePeriod(Guid.NewGuid(), Id, validFromMonth, profile, createdAtUtc));
    }

    public void ChangeVehicleProfile(
        DateOnly validFromMonth,
        VehicleArrangement arrangement,
        DateTimeOffset createdAtUtc)
    {
        ValidateNewPeriod(_vehicleProfiles.Select(item => item.ValidFromMonth), validFromMonth);
        ValidateVehicleArrangement(arrangement);
        _vehicleProfiles.Add(new VehicleProfilePeriod(
            Guid.NewGuid(), Id, validFromMonth, arrangement, createdAtUtc));
    }

    public void OpenTaxYear(int year, DateTimeOffset createdAtUtc)
    {
        if (_taxYears.Any(item => item.Year == year))
        {
            throw new InvalidOperationException($"Rok {year} jest już otwarty.");
        }

        var expectedYear = _taxYears.Max(item => item.Year) + 1;
        if (year != expectedYear)
        {
            throw new InvalidOperationException($"Kolejny rok podatkowy to {expectedYear}.");
        }

        _taxYears.Add(TaxYear.Open(Id, year, TaxationForm.TaxScale, createdAtUtc));
    }

    private static TPeriod ResolvePeriod<TPeriod>(
        IEnumerable<TPeriod> periods,
        DateOnly month,
        Func<TPeriod, DateOnly> validFrom)
    {
        var normalizedMonth = FirstDayOfMonth(month);
        return periods
            .Where(item => validFrom(item) <= normalizedMonth)
            .OrderByDescending(validFrom)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Brak ustawień obowiązujących dla wskazanego miesiąca.");
    }

    private void ValidateNewPeriod(IEnumerable<DateOnly> existingMonths, DateOnly validFromMonth)
    {
        if (validFromMonth.Day != 1)
        {
            throw new ArgumentException("Okres ustawień musi zaczynać się pierwszego dnia miesiąca.", nameof(validFromMonth));
        }

        if (validFromMonth < FirstDayOfMonth(BusinessStartDate))
        {
            throw new ArgumentOutOfRangeException(nameof(validFromMonth));
        }

        var latestMonth = existingMonths.Max();
        if (validFromMonth <= latestMonth)
        {
            throw new InvalidOperationException(
                $"Nowy okres musi być późniejszy niż ostatni zapisany okres ({latestMonth:MM.yyyy}).");
        }
    }

    private static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static void ValidateMonthValue(decimal value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateVatRate(decimal vatRate)
    {
        if (vatRate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate));
        }
    }

    private static void ValidateVehicleArrangement(VehicleArrangement arrangement)
    {
        if (!Enum.IsDefined(arrangement))
        {
            throw new ArgumentOutOfRangeException(nameof(arrangement));
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
