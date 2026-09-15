using System.Net;
using System.Text;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class Phase6PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase6PagesTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cost_review_has_two_explicit_decisions_and_only_document_creates_no_rule()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        var (companyId, documentId) = await SeedCompanyAndDocumentAsync();

        using var prepare = await WebTestSupport.PostFormAsync(
            client,
            $"/Expenses/Review/{documentId}?handler=Prepare",
            new Dictionary<string, string>
            {
                ["Preparation.SellerCountryCode"] = "PL",
                ["Preparation.VatTreatment"] = "DomesticTaxed",
                ["Preparation.VatRate"] = "23",
                ["Preparation.ServiceKind"] = "Ai",
                ["Preparation.GrossAmount"] = "123",
                ["Preparation.InputVatAmount"] = "23",
            });
        Assert.True(
            prepare.StatusCode == HttpStatusCode.Redirect,
            await prepare.Content.ReadAsStringAsync());

        var html = await client.GetStringAsync($"/Expenses/Review/{documentId}");
        Assert.Contains("Zastosuj tylko do tego dokumentu", html, StringComparison.Ordinal);
        Assert.Contains("Zastosuj także do kolejnych", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Decision.VatDeductionPercent\" value=\"50", html, StringComparison.OrdinalIgnoreCase);

        using var confirm = await WebTestSupport.PostFormAsync(
            client,
            $"/Expenses/Review/{documentId}?handler=ConfirmDocument",
            DecisionFields());
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.CostRules.ToListAsync());
        Assert.Single(await db.KpirEntries.ToListAsync());
        Assert.Single(await db.VatPurchaseEntries.ToListAsync());
        Assert.Equal(companyId, (await db.CostBookings.SingleAsync()).CompanyId);

        var books = WebUtility.HtmlDecode(await client.GetStringAsync("/Books?year=2026"));
        Assert.Contains("KPiR", books, StringComparison.Ordinal);
        Assert.Contains("Rejestr VAT zakupów", books, StringComparison.Ordinal);
        Assert.Contains("ręcznie", books, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Vehicle_page_shows_four_independent_categories_without_tax_defaults()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedCompanyAndDocumentAsync(includeDocument: false);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Expenses/VehiclePolicies"));

        Assert.Contains("Najem lub leasing", html, StringComparison.Ordinal);
        Assert.Contains("Eksploatacja", html, StringComparison.Ordinal);
        Assert.Contains("Ubezpieczenie", html, StringComparison.Ordinal);
        Assert.Contains("Publiczne ładowanie", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"50\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nie utworzyła aktywnej polityki", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Charging_page_imports_reference_csv_and_has_no_tax_booking_action()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedCompanyAndDocumentAsync(includeDocument: false);

        using var save = await WebTestSupport.PostFormAsync(
            client,
            "/Expenses/Charging?handler=SaveProfile",
            new Dictionary<string, string>
            {
                ["Profile.Separator"] = ",",
                ["Profile.TimestampColumn"] = "started_at",
                ["Profile.EnergyWhColumn"] = "energy [Wh]",
                ["Profile.DateFormat"] = "yyyy-MM-dd HH:mm:ss",
                ["Profile.TimeZoneId"] = "Europe/Warsaw",
                ["Profile.IdentityColumn"] = "session_id",
            });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        Guid profileId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            profileId = await db.ChargingCsvProfiles.Select(item => item.Id).SingleAsync();
        }

        using var activate = await WebTestSupport.PostFormAsync(
            client,
            "/Expenses/Charging?handler=ActivateProfile",
            new Dictionary<string, string>
            {
                ["profileId"] = profileId.ToString(),
                ["WhConfirmed"] = "true",
            });
        Assert.Equal(HttpStatusCode.Redirect, activate.StatusCode);

        var page = await client.GetStringAsync("/Expenses/Charging?year=2026&month=7");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(WebTestSupport.ExtractAntiforgeryToken(page)), "__RequestVerificationToken");
        upload.Add(new StringContent(profileId.ToString()), "profileId");
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(
            "session_id,device_id,status,started_at,stopped_at,time [s],energy [Wh],power [W], tag NFC, tag NFC ID\n"
            + "A,EVGC011230302GK0215,COMPLETED,2026-07-02 17:26:33,2026-07-02 17:34:14,461,64,498,undefined,undefined\n"
            + "B,EVGC011230302GK0215,COMPLETED,2026-07-02 21:59:59,2026-07-03 03:02:56,18177,42335,8384,undefined,undefined\n"
            + "C,EVGC011230302GK0215,ABORTED,2026-07-25 06:58:22,2026-07-25 07:04:01,339,87,972,undefined,undefined"));
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        upload.Add(bytes, "CsvFile", "synthetic.csv");
        using var import = await client.PostAsync("/Expenses/Charging?handler=Import&year=2026&month=7", upload);
        Assert.Equal(HttpStatusCode.Redirect, import.StatusCode);

        using var report = await WebTestSupport.PostFormAsync(
            client,
            "/Expenses/Charging?handler=GenerateReport&year=2026&month=7",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, report.StatusCode);
        var html = NormalizeSpaces(await client.GetStringAsync("/Expenses/Charging?year=2026&month=7"));
        Assert.Contains("42,486 kWh", html, StringComparison.Ordinal);
        Assert.Contains("38,66 zł", html, StringComparison.Ordinal);
        Assert.Contains("podstawa podatkowa niepotwierdzona", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zaksięguj w KPiR", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dodaj do VAT", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Charging_page_prefills_the_confirmed_home_charger_format()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedCompanyAndDocumentAsync(includeDocument: false);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Expenses/Charging"));

        Assert.Contains("value=\",\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"started_at\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"energy [Wh]\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"yyyy-MM-dd HH:mm:ss\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"session_id\"", html, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> DecisionFields()
        => new()
        {
            ["Decision.KpirCategory"] = "Pozostałe wydatki",
            ["Decision.VatDeductionPercent"] = "50",
            ["Decision.KpirCostPercent"] = "75",
            ["Decision.KpirPeriodPolicy"] = "IssueMonth",
            ["Decision.VatPeriodPolicy"] = "IssueMonth",
            ["Decision.KpirPeriod"] = "2026-09-01",
            ["Decision.VatPeriod"] = "2026-09-01",
            ["Decision.DecisionSource"] = "Sztuczna decyzja testowa",
        };

    private async Task<(Guid CompanyId, Guid DocumentId)> SeedCompanyAndDocumentAsync(bool includeDocument = true)
    {
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = Company.Register(
            owner.Id, "Testowa Firma", "1234563218", "Testowy adres", new DateOnly(2026, 1, 1),
            "Testowy Klient", "1234563218", "Adres klienta", "Testowa usługa",
            1_500m, 23m, 0.91m, VehicleArrangement.None, DateTimeOffset.UtcNow);
        db.Companies.Add(company);
        var documentId = Guid.Empty;
        if (includeDocument)
        {
            var document = SourceDocument.CreateManual(Guid.NewGuid(), owner.Id, DateTimeOffset.UtcNow);
            document.AttachSource(Guid.NewGuid(), new string('A', 64), DateTimeOffset.UtcNow);
            document.ApplyExtraction(DocumentData.Empty, null, DateTimeOffset.UtcNow);
            document.ConfirmData(new DocumentData(
                "FV/1/2026", "Testowy Dostawca", "PL123", new DateOnly(2026, 9, 1), 123m, "PLN"),
                DateTimeOffset.UtcNow);
            db.SourceDocuments.Add(document);
            documentId = document.Id;
        }

        await db.SaveChangesAsync();
        return (company.Id, documentId);
    }

    private static string NormalizeSpaces(string html)
        => WebUtility.HtmlDecode(html)
            .Replace('\u00a0', ' ')
            .Replace('\u202f', ' ');
}
