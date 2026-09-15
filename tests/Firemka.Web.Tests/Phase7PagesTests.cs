using System.Net;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class Phase7PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private const int Year = 2026;
    private const int Month = 8;
    private static readonly DateOnly August = new(Year, Month, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase7PagesTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Month?year=2026&month=8")]
    [InlineData("/Settlements/Month?year=2026&month=8")]
    [InlineData("/Settings/Calculations")]
    public async Task Phase7_pages_require_an_authenticated_owner(string path)
    {
        using var client = WebTestSupport.CreateCookieClient(_factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(response));
    }

    [Fact]
    public async Task Dashboard_and_details_show_technical_estimates_blocker_links_and_owner_scoped_counts()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedScenarioAsync(includeIssuedSale: true, includeForeignDocumentNoise: true);

        var dashboard = Normalize(await client.GetStringAsync("/Month?year=2026&month=8"));

        Assert.Contains("Szacunek PIT", dashboard, StringComparison.Ordinal);
        Assert.Contains("Szacunek VAT", dashboard, StringComparison.Ordinal);
        Assert.Contains("Składka zdrowotna", dashboard, StringComparison.Ordinal);
        Assert.Contains("Szacunek techniczny — niezatwierdzone zasady", dashboard, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"documents-to-review\">0<", dashboard, StringComparison.Ordinal);
        Assert.Contains("Zasady są tylko materiałem referencyjnym", dashboard, StringComparison.Ordinal);
        Assert.Contains("/Settlements/Month?year=2026&month=8", dashboard, StringComparison.Ordinal);

        var details = Normalize(await client.GetStringAsync("/Settlements/Month?year=2026&month=8"));
        Assert.Contains("Źródła i wyjaśnienie", details, StringComparison.Ordinal);
        Assert.Contains("PIT, VAT i zdrowotna", details, StringComparison.Ordinal);
        Assert.Contains("Braki do rozwiązania", details, StringComparison.Ordinal);
        Assert.Contains("Zamknięcie i historia", details, StringComparison.Ordinal);
        Assert.Contains("Uzupełnij i potwierdź dane miesięczne", details, StringComparison.Ordinal);
        Assert.Contains("/Settings/Calculations", details, StringComparison.Ordinal);
        Assert.DoesNotContain("Wyślij JPK", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Opłać zobowiązanie", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zamknij rok", details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Settlement_page_loads_polish_decimal_validation_and_csp_safe_navigation()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedScenarioAsync(includeIssuedSale: false);

        var html = Normalize(await client.GetStringAsync("/Settlements/Month?year=2026&month=8"));

        Assert.Contains("/js/firemka-validation.", html, StringComparison.Ordinal);
        Assert.Contains("data-navigation-toggle", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-navigation\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bs-toggle=\"collapse\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("style=\"display:none\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"SalesRecognition\"", html, StringComparison.Ordinal);

        var settings = Normalize(await client.GetStringAsync("/Settings"));
        Assert.Contains("id=\"vat\"", settings, StringComparison.Ordinal);
        Assert.Contains("id=\"zus\"", settings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declaration_validation_preserves_comma_decimals_and_valid_post_saves_them()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedScenarioAsync(includeIssuedSale: true);
        var path = "/Settlements/Month?year=2026&month=8&handler=Declaration";

        using var invalid = await WebTestSupport.PostFormAsync(
            client,
            path,
            DeclarationFields(evidence: string.Empty));
        var invalidHtml = Normalize(await invalid.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Podaj źródło lub opis potwierdzenia", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"123,45\"", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"12,34\"", invalidHtml, StringComparison.Ordinal);

        using var valid = await WebTestSupport.PostFormAsync(
            client,
            path,
            DeclarationFields(evidence: "synthetic:owner-month-check"));
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var declaration = await db.MonthDeclarations.SingleAsync();
        Assert.Equal(123.45m, declaration.SocialContributionsDeductible);
        Assert.Equal(12.34m, declaration.PitBaseAdjustment);
        Assert.Equal(45.67m, declaration.HealthIncomeAdjustment);
        Assert.True(declaration.OpeningBalancesConfirmed);
        Assert.True(declaration.HealthIncomeConfirmed);
    }

    [Fact]
    public async Task Calculation_rules_need_checkbox_evidence_and_date_before_version_two_is_confirmed()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedScenarioAsync(includeIssuedSale: false);

        var initial = Normalize(await client.GetStringAsync("/Settings/Calculations"));
        Assert.Contains("Materiał referencyjny — wymaga niezależnego potwierdzenia", initial, StringComparison.Ordinal);
        Assert.Contains("120 000,00", initial, StringComparison.Ordinal);
        Assert.Contains("podatki.gov.pl", initial, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zus.pl", initial, StringComparison.OrdinalIgnoreCase);

        using var invalid = await WebTestSupport.PostFormAsync(
            client,
            "/Settings/Calculations?handler=Confirm",
            new Dictionary<string, string>
            {
                ["Confirmation.IndependentVerificationConfirmed"] = "false",
                ["Confirmation.EvidenceReference"] = string.Empty,
                ["Confirmation.ConfirmedOn"] = "2026-09-12",
            });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = Normalize(await invalid.Content.ReadAsStringAsync());
        Assert.Contains("Zaznacz niezależną weryfikację", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("Podaj odniesienie do niezależnej weryfikacji", invalidHtml, StringComparison.Ordinal);

        using var valid = await WebTestSupport.PostFormAsync(
            client,
            "/Settings/Calculations?handler=Confirm",
            new Dictionary<string, string>
            {
                ["Confirmation.IndependentVerificationConfirmed"] = "true",
                ["Confirmation.EvidenceReference"] = "synthetic:independent-adviser-check",
                ["Confirmation.ConfirmedOn"] = "2026-09-12",
            });
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versions = await db.CalculationRuleSets.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(CalculationRuleTrust.ReferenceOnly, versions[0].Trust);
        Assert.Equal(CalculationRuleTrust.IndependentlyConfirmed, versions[1].Trust);
        Assert.Equal("synthetic:independent-adviser-check", versions[1].IndependentEvidenceReference);

        var stillIncomplete = Normalize(await client.GetStringAsync("/Settlements/Month?year=2026&month=8"));
        Assert.Contains("Szacunek roboczy — są jeszcze braki do uzupełnienia", stillIncomplete, StringComparison.Ordinal);
        Assert.DoesNotContain("Szacunek techniczny — niezatwierdzone zasady", stillIncomplete, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Owner_can_close_then_open_and_close_a_correction_without_overwriting_history()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        var companyId = await SeedScenarioAsync(includeIssuedSale: true);

        using (var confirm = await WebTestSupport.PostFormAsync(
            client,
            "/Settings/Calculations?handler=Confirm",
            new Dictionary<string, string>
            {
                ["Confirmation.IndependentVerificationConfirmed"] = "true",
                ["Confirmation.EvidenceReference"] = "synthetic:independent-adviser-check",
                ["Confirmation.ConfirmedOn"] = "2026-09-12",
            }))
        {
            Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        }

        var detailsPath = "/Settlements/Month?year=2026&month=8";
        using (var declaration = await WebTestSupport.PostFormAsync(
            client,
            detailsPath + "&handler=Declaration",
            DeclarationFields(evidence: "synthetic:month-ready", allZero: true)))
        {
            Assert.Equal(HttpStatusCode.Redirect, declaration.StatusCode);
        }

        var ready = await client.GetStringAsync(detailsPath);
        Assert.Contains("data-testid=\"close-month\"", ready, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"close-month\" disabled", ready, StringComparison.Ordinal);

        using (var close = await WebTestSupport.PostFormAsync(
            client,
            detailsPath + "&handler=Close",
            new Dictionary<string, string>()))
        {
            Assert.Equal(HttpStatusCode.Redirect, close.StatusCode);
        }

        await SeedAdditionalIssuedSaleAsync(companyId);
        var drift = await client.GetStringAsync(detailsPath);
        Assert.Contains("Dane źródłowe zmieniły się", drift, StringComparison.Ordinal);
        Assert.Contains("Rozpocznij korektę", drift, StringComparison.Ordinal);

        using (var correction = await WebTestSupport.PostFormAsync(
            client,
            detailsPath + "&handler=Correction",
            new Dictionary<string, string>
            {
                ["Correction.Reason"] = "Syntetyczna dodatkowa sprzedaż",
            }))
        {
            Assert.Equal(HttpStatusCode.Redirect, correction.StatusCode);
        }

        using (var adjustment = await WebTestSupport.PostFormAsync(
            client,
            detailsPath + "&handler=Adjustment",
            new Dictionary<string, string>
            {
                ["Adjustment.Kind"] = "HealthIncome",
                ["Adjustment.Amount"] = "10,50",
                ["Adjustment.Reason"] = "Syntetyczne doprecyzowanie podstawy zdrowotnej",
                ["Adjustment.EvidenceReference"] = "synthetic:health-bridge-check",
            }))
        {
            Assert.Equal(HttpStatusCode.Redirect, adjustment.StatusCode);
        }

        using (var closeCorrection = await WebTestSupport.PostFormAsync(
            client,
            detailsPath + "&handler=Close",
            new Dictionary<string, string>()))
        {
            Assert.Equal(HttpStatusCode.Redirect, closeCorrection.StatusCode);
        }

        var history = Normalize(await client.GetStringAsync(detailsPath));
        Assert.Contains("Wersja 1", history, StringComparison.Ordinal);
        Assert.Contains("Wersja 2", history, StringComparison.Ordinal);
        Assert.Contains("Syntetyczna dodatkowa sprzedaż", history, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.MonthCalculations.CountAsync());
        var settlements = await db.MonthSettlements.OrderBy(item => item.VersionNumber).ToListAsync();
        Assert.Equal(2, settlements.Count);
        Assert.All(settlements, item => Assert.Equal(MonthSettlementStatus.Closed, item.Status));
        Assert.Equal(settlements[0].Id, settlements[1].PreviousSettlementId);
        Assert.Equal("Syntetyczna dodatkowa sprzedaż", settlements[1].CorrectionReason);
    }

    private static Dictionary<string, string> DeclarationFields(
        string evidence,
        bool allZero = false)
        => new()
        {
            ["Declaration.SocialContributionsDeductible"] = allZero ? "0" : "123,45",
            ["Declaration.PitBaseAdjustment"] = allZero ? "0" : "12,34",
            ["Declaration.HealthIncomeAdjustment"] = allZero ? "0" : "45,67",
            ["Declaration.OpeningVatCarryForward"] = "0",
            ["Declaration.OpeningPitAdvancesDue"] = "0",
            ["Declaration.OpeningBalancesConfirmed"] = "true",
            ["Declaration.HealthIncomeConfirmed"] = "true",
            ["Declaration.EvidenceReference"] = evidence,
        };

    private async Task<Guid> SeedScenarioAsync(
        bool includeIssuedSale,
        bool includeForeignDocumentNoise = false)
    {
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = Company.Register(
            owner.Id,
            "Testowa Firma Fazy 7",
            "1234563218",
            "Testowy adres 1, 00-001 Warszawa",
            August,
            "Syntetyczny Klient",
            "1234563218",
            "Testowy adres 2, 00-002 Warszawa",
            "Syntetyczna usługa abonamentowa",
            10_000m,
            23m,
            0.91m,
            VehicleArrangement.None,
            Now);
        db.Companies.Add(company);
        if (includeIssuedSale)
        {
            db.SalesInvoices.Add(CreateIssuedSale(company, 10_000m, "FV/TEST/1"));
        }

        if (includeForeignDocumentNoise)
        {
            db.Companies.Add(Company.Register(
                "foreign-owner",
                "Obca Firma",
                "9876543210",
                "Obcy adres",
                August,
                "Obcy klient",
                "9876543210",
                "Obcy adres klienta",
                "Obca usługa",
                1_000m,
                23m,
                1m,
                VehicleArrangement.None,
                Now));
            var foreignDocument = SourceDocument.CreateManual(Guid.NewGuid(), "foreign-owner", Now.AddDays(-20));
            foreignDocument.AttachSource(Guid.NewGuid(), new string('B', 64), Now.AddDays(-20));
            foreignDocument.ApplyExtraction(DocumentData.Empty, null, Now.AddDays(-20));
            db.SourceDocuments.Add(foreignDocument);
        }

        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task SeedAdditionalIssuedSaleAsync(Guid companyId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await db.Companies.Include(item => item.SubscriptionRates).SingleAsync(item => item.Id == companyId);
        db.SalesInvoices.Add(CreateIssuedSale(company, 100m, "FV/TEST/2"));
        await db.SaveChangesAsync();
    }

    private static SalesInvoice CreateIssuedSale(Company company, decimal net, string number)
    {
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            company.OwnerUserId,
            August,
            company.GetSubscriptionRate(August).Id,
            company.ServiceDescription,
            net,
            23m,
            Now);
        invoice.PrepareForIssue(August.AddDays(1), number, false, "<Invoice />", Now);
        invoice.RegisterSubmission($"session-{number}", $"submission-{number}", Now);
        invoice.MarkIssued($"KSEF-{number}", "<UPO />", "<Invoice />", Now);
        return invoice;
    }

    private static string Normalize(string html)
        => WebUtility.HtmlDecode(html)
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace('\u00a0', ' ')
            .Replace('\u202f', ' ');
}
