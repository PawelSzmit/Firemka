using System.Net;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class CompanyOnboardingTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public CompanyOnboardingTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_can_finish_company_wizard_and_open_month_dashboard()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);

        using var beforeSetup = await client.GetAsync("/Month");
        var beforeSetupHtml = await beforeSetup.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, beforeSetup.StatusCode);
        Assert.Contains("Dokończ konfigurację firmy", beforeSetupHtml);

        using var setupResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Setup/Company",
            new Dictionary<string, string>
            {
                ["Input.CompanyName"] = "Testowa Firma",
                ["Input.CompanyNip"] = "1234563218",
                ["Input.CompanyAddress"] = "Testowy adres firmy 2, 00-002 Warszawa",
                ["Input.BusinessStartDate"] = "2026-10-15",
                ["Input.CounterpartyName"] = "Testowy Klient",
                ["Input.CounterpartyNip"] = "1234563218",
                ["Input.CounterpartyAddress"] = "Testowy adres 1, 00-001 Warszawa",
                ["Input.ServiceDescription"] = "Miesięczny dostęp do aplikacji Test SaaS",
                ["Input.SubscriptionNetMonthlyAmount"] = "1500",
                ["Input.SubscriptionVatRate"] = "23",
                ["Input.EnergyGrossPricePerKwh"] = "0.91",
                ["Input.VehicleArrangement"] = "None",
            });

        Assert.Equal(HttpStatusCode.Redirect, setupResponse.StatusCode);
        Assert.Equal("/Month", WebTestSupport.GetLocationPath(setupResponse));

        using var dashboard = await client.GetAsync("/Month?year=2026&month=10");
        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        var normalizedDashboardHtml = dashboardHtml
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace("&#xA0;", " ", StringComparison.Ordinal)
            .Replace('\u00a0', ' ');
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Contains("Testowa Firma", dashboardHtml);
        Assert.Contains("1 500,00", normalizedDashboardHtml);
        Assert.Contains("Szacunek techniczny — niezatwierdzone zasady", dashboardHtml);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await dbContext.Companies
            .Include(item => item.TaxYears)
            .Include(item => item.SubscriptionRates)
            .SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 15), company.BusinessStartDate);
        Assert.Equal("Testowy adres firmy 2, 00-002 Warszawa", company.Address);
        Assert.Equal("Miesięczny dostęp do aplikacji Test SaaS", company.ServiceDescription);
        Assert.Equal(new DateOnly(2026, 10, 1), Assert.Single(company.SubscriptionRates).ValidFromMonth);
        Assert.Equal(1500m, company.GetSubscriptionRate(new DateOnly(2026, 10, 1)).NetMonthlyAmount);
        Assert.Single(company.TaxYears);
    }

    [Fact]
    public async Task Wizard_exposes_only_tax_scale_and_validates_required_values()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);

        using var page = await client.GetAsync("/Setup/Company");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Skala podatkowa", html);
        Assert.DoesNotContain("Ryczałt", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Input.TaxationForm", html);

        using var invalid = await WebTestSupport.PostFormAsync(
            client,
            "/Setup/Company",
            new Dictionary<string, string>
            {
                ["Input.CompanyName"] = string.Empty,
                ["Input.CompanyNip"] = string.Empty,
                ["Input.CompanyAddress"] = string.Empty,
                ["Input.BusinessStartDate"] = string.Empty,
                ["Input.CounterpartyName"] = string.Empty,
                ["Input.CounterpartyNip"] = string.Empty,
                ["Input.CounterpartyAddress"] = string.Empty,
                ["Input.ServiceDescription"] = string.Empty,
                ["Input.SubscriptionNetMonthlyAmount"] = string.Empty,
                ["Input.SubscriptionVatRate"] = string.Empty,
                ["Input.EnergyGrossPricePerKwh"] = string.Empty,
                ["Input.VehicleArrangement"] = "None",
            });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await dbContext.Companies.AnyAsync());
    }

    [Fact]
    public async Task Future_settings_period_and_new_year_leave_previous_month_unchanged()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await RegisterCompanyAsync(client);

        using var subscriptionResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=Subscription",
            new Dictionary<string, string>
            {
                ["Subscription.ValidFromMonth"] = "2027-01-01",
                ["Subscription.NetMonthlyAmount"] = "1800",
                ["Subscription.VatRate"] = "23",
            });
        Assert.True(
            subscriptionResponse.StatusCode == HttpStatusCode.Redirect,
            await subscriptionResponse.Content.ReadAsStringAsync());

        using var yearResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=OpenYear",
            new Dictionary<string, string> { ["NewTaxYear.Year"] = "2027" });
        Assert.True(
            yearResponse.StatusCode == HttpStatusCode.Redirect,
            await yearResponse.Content.ReadAsStringAsync());

        using var december = await client.GetAsync("/Month?year=2026&month=12");
        using var january = await client.GetAsync("/Month?year=2027&month=1");
        var decemberHtml = NormalizeSpaces(await december.Content.ReadAsStringAsync());
        var januaryHtml = NormalizeSpaces(await january.Content.ReadAsStringAsync());
        Assert.Contains("1 500,00", decemberHtml);
        Assert.Contains("1 800,00", januaryHtml);

        using var settings = await client.GetAsync("/Settings");
        var settingsHtml = await settings.Content.ReadAsStringAsync();
        Assert.Contains("2026", settingsHtml);
        Assert.Contains("2027", settingsHtml);
        Assert.DoesNotContain("Ryczałt", settingsHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Settings_reject_a_period_that_does_not_start_on_the_first_day_of_a_month()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await RegisterCompanyAsync(client);

        using var response = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=Subscription",
            new Dictionary<string, string>
            {
                ["Subscription.ValidFromMonth"] = "2027-01-15",
                ["Subscription.NetMonthlyAmount"] = "1800",
                ["Subscription.VatRate"] = "23",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "pierwszego dnia miesiąca",
            responseHtml,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0001-01-01", responseHtml);
        Assert.DoesNotContain("name=\"NewTaxYear.Year\" value=\"0\"", responseHtml);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await dbContext.Companies.Include(item => item.SubscriptionRates).SingleAsync();
        Assert.Single(company.SubscriptionRates);
    }

    [Fact]
    public async Task Owner_can_add_future_vat_zus_and_vehicle_periods_without_changing_the_previous_month()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await RegisterCompanyAsync(client);

        using var vatResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=Vat",
            new Dictionary<string, string>
            {
                ["Vat.ValidFromMonth"] = "2027-01-01",
                ["Vat.Profile"] = "VatExempt",
            });
        using var zusResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=Zus",
            new Dictionary<string, string>
            {
                ["Zus.ValidFromMonth"] = "2027-01-01",
                ["Zus.Profile"] = "SocialAndHealthContributions",
            });
        using var vehicleResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=Vehicle",
            new Dictionary<string, string>
            {
                ["Vehicle.ValidFromMonth"] = "2027-01-01",
                ["Vehicle.Arrangement"] = "LongTermRental",
            });

        Assert.Equal(HttpStatusCode.Redirect, vatResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, zusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, vehicleResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await dbContext.Companies
            .Include(item => item.VatProfiles)
            .Include(item => item.ZusProfiles)
            .Include(item => item.VehicleProfiles)
            .SingleAsync();
        Assert.Equal(2, company.VatProfiles.Count);
        Assert.Equal(2, company.ZusProfiles.Count);
        Assert.Equal(2, company.VehicleProfiles.Count);
        Assert.Equal(
            Firemka.Domain.Companies.VatProfile.ActiveMonthly,
            company.GetVatProfile(new DateOnly(2026, 12, 1)).Profile);
        Assert.Equal(
            Firemka.Domain.Companies.VatProfile.VatExempt,
            company.GetVatProfile(new DateOnly(2027, 1, 1)).Profile);
    }

    [Fact]
    public async Task Settings_reject_skipping_a_tax_year()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await RegisterCompanyAsync(client);

        using var response = await WebTestSupport.PostFormAsync(
            client,
            "/Settings?handler=OpenYear",
            new Dictionary<string, string> { ["NewTaxYear.Year"] = "2028" });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("2027", html);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await dbContext.Set<Firemka.Domain.TaxYears.TaxYear>().CountAsync());
    }

    private static Task<HttpResponseMessage> RegisterCompanyAsync(HttpClient client)
        => WebTestSupport.PostFormAsync(
            client,
            "/Setup/Company",
            new Dictionary<string, string>
            {
                ["Input.CompanyName"] = "Testowa Firma",
                ["Input.CompanyNip"] = "1234563218",
                ["Input.CompanyAddress"] = "Testowy adres firmy 2, 00-002 Warszawa",
                ["Input.BusinessStartDate"] = "2026-10-15",
                ["Input.CounterpartyName"] = "Testowy Klient",
                ["Input.CounterpartyNip"] = "1234563218",
                ["Input.CounterpartyAddress"] = "Testowy adres 1, 00-001 Warszawa",
                ["Input.ServiceDescription"] = "Miesięczny dostęp do aplikacji Test SaaS",
                ["Input.SubscriptionNetMonthlyAmount"] = "1500",
                ["Input.SubscriptionVatRate"] = "23",
                ["Input.EnergyGrossPricePerKwh"] = "0.91",
                ["Input.VehicleArrangement"] = "None",
            });

    private static string NormalizeSpaces(string html)
        => html
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace("&#xA0;", " ", StringComparison.Ordinal)
            .Replace('\u00a0', ' ');
}
