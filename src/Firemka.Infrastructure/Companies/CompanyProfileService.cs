using Firemka.Application.Onboarding;
using Firemka.Domain.Companies;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Companies;

public sealed class CompanyProfileService(AppDbContext dbContext) : ICompanyProfileService
{
    public Task<bool> ExistsAsync(string ownerUserId, CancellationToken cancellationToken = default)
        => dbContext.Companies.AnyAsync(item => item.OwnerUserId == ownerUserId, cancellationToken);

    public async Task RegisterAsync(
        RegisterCompanyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (await ExistsAsync(command.OwnerUserId, cancellationToken))
        {
            throw new InvalidOperationException("Firma dla tego konta została już skonfigurowana.");
        }

        var company = Company.Register(
            command.OwnerUserId,
            command.CompanyName,
            command.CompanyNip,
            command.CompanyAddress,
            command.BusinessStartDate,
            command.CounterpartyName,
            command.CounterpartyNip,
            command.CounterpartyAddress,
            command.ServiceDescription,
            command.SubscriptionNetMonthlyAmount,
            command.SubscriptionVatRate,
            command.EnergyGrossPricePerKwh,
            command.VehicleArrangement,
            command.CreatedAtUtc);
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CompanyProfileSnapshot?> GetAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var company = await QueryCompany(ownerUserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return company is null ? null : ToSnapshot(company);
    }

    public async Task ChangeSubscriptionRateAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        decimal netMonthlyAmount,
        decimal vatRate,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.ChangeSubscriptionRate(validFromMonth, netMonthlyAmount, vatRate, createdAtUtc);
        var newPeriod = company.SubscriptionRates.Single(item => item.ValidFromMonth == validFromMonth);
        dbContext.Entry(newPeriod).State = EntityState.Added;
        var affectedDrafts = await dbContext.SalesInvoices
            .Where(item => item.CompanyId == company.Id
                && item.Status == SalesInvoiceStatus.Draft
                && item.ServiceMonth >= validFromMonth)
            .ToListAsync(cancellationToken);
        foreach (var draft in affectedDrafts)
        {
            draft.ApplySubscriptionRate(
                newPeriod.Id,
                newPeriod.NetMonthlyAmount,
                newPeriod.VatRate,
                replaceManualOverride: false,
                createdAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeEnergyRateAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        decimal grossPricePerKwh,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.ChangeEnergyRate(validFromMonth, grossPricePerKwh, createdAtUtc);
        var newPeriod = company.EnergyRates.Single(item => item.ValidFromMonth == validFromMonth);
        dbContext.Entry(newPeriod).State = EntityState.Added;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task OpenTaxYearAsync(
        string ownerUserId,
        int year,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.OpenTaxYear(year, createdAtUtc);
        var newYear = company.TaxYears.Single(item => item.Year == year);
        dbContext.Entry(newYear).State = EntityState.Added;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeVatProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        VatProfile profile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.ChangeVatProfile(validFromMonth, profile, createdAtUtc);
        var newPeriod = company.VatProfiles.Single(item => item.ValidFromMonth == validFromMonth);
        dbContext.Entry(newPeriod).State = EntityState.Added;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeZusProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        ZusProfile profile,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.ChangeZusProfile(validFromMonth, profile, createdAtUtc);
        var newPeriod = company.ZusProfiles.Single(item => item.ValidFromMonth == validFromMonth);
        dbContext.Entry(newPeriod).State = EntityState.Added;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeVehicleProfileAsync(
        string ownerUserId,
        DateOnly validFromMonth,
        VehicleArrangement arrangement,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTrackedCompanyAsync(ownerUserId, cancellationToken);
        company.ChangeVehicleProfile(validFromMonth, arrangement, createdAtUtc);
        var newPeriod = company.VehicleProfiles.Single(item => item.ValidFromMonth == validFromMonth);
        dbContext.Entry(newPeriod).State = EntityState.Added;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Company> QueryCompany(string ownerUserId)
        => dbContext.Companies
            .Where(item => item.OwnerUserId == ownerUserId)
            .Include(item => item.Counterparty)
            .Include(item => item.TaxYears)
            .Include(item => item.SubscriptionRates)
            .Include(item => item.EnergyRates)
            .Include(item => item.VatProfiles)
            .Include(item => item.ZusProfiles)
            .Include(item => item.VehicleProfiles)
            .AsSplitQuery();

    private async Task<Company> GetTrackedCompanyAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
        => await QueryCompany(ownerUserId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Firma nie została jeszcze skonfigurowana.");

    private static CompanyProfileSnapshot ToSnapshot(Company company)
        => new(
            company.Id,
            company.Name,
            company.Nip,
            company.Address,
            company.BusinessStartDate,
            company.Counterparty.Name,
            company.Counterparty.Nip,
            company.Counterparty.Address,
            company.ServiceDescription,
            company.TaxYears
                .OrderBy(item => item.Year)
                .Select(item => new TaxYearSnapshot(item.Year, item.TaxationForm))
                .ToArray(),
            company.SubscriptionRates
                .OrderBy(item => item.ValidFromMonth)
                .Select(item => new SubscriptionRateSnapshot(
                    item.ValidFromMonth,
                    item.NetMonthlyAmount,
                    item.VatRate))
                .ToArray(),
            company.EnergyRates
                .OrderBy(item => item.ValidFromMonth)
                .Select(item => new EnergyRateSnapshot(item.ValidFromMonth, item.GrossPricePerKwh))
                .ToArray(),
            company.VatProfiles
                .OrderBy(item => item.ValidFromMonth)
                .Select(item => new VatProfileSnapshot(item.ValidFromMonth, item.Profile))
                .ToArray(),
            company.ZusProfiles
                .OrderBy(item => item.ValidFromMonth)
                .Select(item => new ZusProfileSnapshot(item.ValidFromMonth, item.Profile))
                .ToArray(),
            company.VehicleProfiles
                .OrderBy(item => item.ValidFromMonth)
                .Select(item => new VehicleProfileSnapshot(item.ValidFromMonth, item.Arrangement))
                .ToArray());
}
