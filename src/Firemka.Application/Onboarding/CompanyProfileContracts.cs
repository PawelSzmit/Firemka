using Firemka.Domain.Companies;
using Firemka.Domain.TaxYears;

namespace Firemka.Application.Onboarding;

public sealed record RegisterCompanyCommand(
    string OwnerUserId,
    string CompanyName,
    string CompanyNip,
    string CompanyAddress,
    DateOnly BusinessStartDate,
    string CounterpartyName,
    string CounterpartyNip,
    string CounterpartyAddress,
    string ServiceDescription,
    decimal SubscriptionNetMonthlyAmount,
    decimal SubscriptionVatRate,
    decimal EnergyGrossPricePerKwh,
    VehicleArrangement VehicleArrangement,
    DateTimeOffset CreatedAtUtc);

public sealed record CompanyProfileSnapshot(
    Guid Id,
    string CompanyName,
    string CompanyNip,
    string CompanyAddress,
    DateOnly BusinessStartDate,
    string CounterpartyName,
    string CounterpartyNip,
    string CounterpartyAddress,
    string ServiceDescription,
    IReadOnlyList<TaxYearSnapshot> TaxYears,
    IReadOnlyList<SubscriptionRateSnapshot> SubscriptionRates,
    IReadOnlyList<EnergyRateSnapshot> EnergyRates,
    IReadOnlyList<VatProfileSnapshot> VatProfiles,
    IReadOnlyList<ZusProfileSnapshot> ZusProfiles,
    IReadOnlyList<VehicleProfileSnapshot> VehicleProfiles);

public sealed record TaxYearSnapshot(int Year, TaxationForm TaxationForm);

public sealed record SubscriptionRateSnapshot(
    DateOnly ValidFromMonth,
    decimal NetMonthlyAmount,
    decimal VatRate);

public sealed record EnergyRateSnapshot(DateOnly ValidFromMonth, decimal GrossPricePerKwh);

public sealed record VatProfileSnapshot(DateOnly ValidFromMonth, VatProfile Profile);

public sealed record ZusProfileSnapshot(DateOnly ValidFromMonth, ZusProfile Profile);

public sealed record VehicleProfileSnapshot(DateOnly ValidFromMonth, VehicleArrangement Arrangement);

public interface ICompanyProfileService
{
    Task<bool> ExistsAsync(string ownerUserId, CancellationToken cancellationToken = default);

    Task RegisterAsync(RegisterCompanyCommand command, CancellationToken cancellationToken = default);

    Task<CompanyProfileSnapshot?> GetAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task ChangeSubscriptionRateAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        decimal netMonthlyAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task ChangeEnergyRateAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        decimal grossPricePerKwh,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task ChangeVatProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        VatProfile profile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task ChangeZusProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        ZusProfile profile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task ChangeVehicleProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        VehicleArrangement arrangement,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task OpenTaxYearAsync(
        string ownerUserId,
        int year,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);
}
