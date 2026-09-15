using System.Net;
using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class Phase8PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase8PagesTests(FiremkaWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/Settings/Filings")]
    [InlineData("/Settlements/Filings?year=2026&month=8")]
    public async Task Filing_pages_require_login(string path)
    {
        using var client = WebTestSupport.CreateCookieClient(_factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(response));
    }

    [Fact]
    public async Task Owner_confirms_profile_generates_three_files_and_must_approve_before_download()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        var companyId = await SeedClosedAugustAsync();

        var settings = await client.GetStringAsync("/Settings/Filings");
        Assert.Contains("nie potwierdza, czy kod urzędu", settings, StringComparison.OrdinalIgnoreCase);
        using (var future = await WebTestSupport.PostFormAsync(
            client,
            "/Settings/Filings",
            new Dictionary<string, string>
            {
                ["Input.FirstName"] = "Jan",
                ["Input.LastName"] = "Testowy",
                ["Input.BirthDate"] = "1990-01-01",
                ["Input.Pesel"] = "90010112345",
                ["Input.TaxOfficeCode"] = "1215",
                ["Input.ZusInsuranceTitleCode"] = "0510",
                ["Input.Email"] = "jan@example.test",
                ["Input.ConfirmationEvidence"] = "synthetic:future-check",
                ["Input.ConfirmedOn"] = "2099-09-13",
                ["Input.IndependentCheckConfirmed"] = "true",
            }))
        {
            Assert.Equal(HttpStatusCode.OK, future.StatusCode);
            Assert.Contains("przyszłości", WebUtility.HtmlDecode(await future.Content.ReadAsStringAsync()),
                StringComparison.OrdinalIgnoreCase);
        }
        using (var save = await WebTestSupport.PostFormAsync(
            client,
            "/Settings/Filings",
            new Dictionary<string, string>
            {
                ["Input.FirstName"] = "Jan",
                ["Input.LastName"] = "Testowy",
                ["Input.BirthDate"] = "1990-01-01",
                ["Input.Pesel"] = "90010112345",
                ["Input.TaxOfficeCode"] = "1215",
                ["Input.ZusInsuranceTitleCode"] = "0510",
                ["Input.Email"] = "jan@example.test",
                ["Input.ConfirmationEvidence"] = "synthetic:official-data-check",
                ["Input.ConfirmedOn"] = "2026-09-12",
                ["Input.IndependentCheckConfirmed"] = "true",
            }))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        const string filingsPath = "/Settlements/Filings?year=2026&month=8";
        var before = await client.GetStringAsync(filingsPath);
        Assert.Contains("Firemka przygotowuje i sprawdza pliki, ale nie łączy się", before, StringComparison.Ordinal);
        using (var generate = await WebTestSupport.PostFormAsync(
            client,
            filingsPath + "&handler=Generate",
            new Dictionary<string, string>()))
        {
            Assert.Equal(HttpStatusCode.Redirect, generate.StatusCode);
        }

        Guid vatId;
        Guid vatFileId;
        Guid unapprovedPkpirFileId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var artifacts = await db.FilingArtifacts.OrderBy(item => item.Kind).ToListAsync();
            Assert.Equal(3, artifacts.Count);
            var vat = artifacts.Single(item => item.Kind == FilingArtifactKind.JpkV7M3);
            vatId = vat.Id;
            vatFileId = vat.StoredFileId;
            unapprovedPkpirFileId = artifacts.Single(item => item.Kind == FilingArtifactKind.JpkPkpir3).StoredFileId;
            Assert.All(artifacts, item => Assert.Equal(companyId, item.CompanyId));
        }

        using (var blocked = await client.GetAsync($"/Files/{unapprovedPkpirFileId}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);
        }
        var generatedPage = WebUtility.HtmlDecode(await client.GetStringAsync(filingsPath));
        Assert.Contains("JPK_V7M(3)", generatedPage, StringComparison.Ordinal);
        Assert.Contains("JPK_PKPIR(3) narastająco", generatedPage, StringComparison.Ordinal);
        Assert.Contains("ZUS DRA — KEDU 2.27", generatedPage, StringComparison.Ordinal);

        using (var approve = await WebTestSupport.PostFormAsync(
            client,
            filingsPath + "&handler=Approve",
            new Dictionary<string, string>
            {
                ["Action.ArtifactId"] = vatId.ToString(),
                ["Action.Reference"] = "Porównano z zamknięciem miesiąca",
            }))
        {
            Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);
        }
        using var download = await client.GetAsync($"/Files/{vatFileId}?download=true");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.NotNull(download.Content.Headers.ContentDisposition?.FileNameStar
            ?? download.Content.Headers.ContentDisposition?.FileName);

        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using (var actionScope = _factory.Services.CreateAsyncScope())
        {
            var service = actionScope.ServiceProvider.GetRequiredService<IFilingService>();
            await service.MarkSentAsync(
                owner.Id, vatId, "Klient JPK WEB — wysłano ręcznie", Now.AddMinutes(2));
            await service.RecordOutcomeAsync(
                owner.Id,
                vatId,
                FilingSubmissionOutcome.Accepted,
                new FilingReceiptUpload(
                    "upo.xml", "application/xml", new MemoryStream("<UPO>syntetyczne</UPO>"u8.ToArray())),
                "UPO-FAZA-8",
                Now.AddMinutes(3));
        }

        var historyPage = WebUtility.HtmlDecode(await client.GetStringAsync(filingsPath));
        Assert.Contains("Zatwierdzono", historyPage, StringComparison.Ordinal);
        Assert.Contains("Porównano z zamknięciem miesiąca", historyPage, StringComparison.Ordinal);
        Assert.Contains("Wysłano ręcznie", historyPage, StringComparison.Ordinal);
        Assert.Contains("Klient JPK WEB — wysłano ręcznie", historyPage, StringComparison.Ordinal);
        Assert.Contains("Wynik zapisano", historyPage, StringComparison.Ordinal);
        Assert.Contains("UPO-FAZA-8", historyPage, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedClosedAugustAsync()
    {
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = Company.Register(
            owner.Id,
            "Testowa Firma",
            "1010000000",
            "Testowy adres firmy",
            August,
            "Syntetyczny Klient",
            "1234567890",
            "Adres testowego klienta",
            "Usługa testowa",
            1_000m,
            23m,
            1m,
            VehicleArrangement.None,
            Now);
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            owner.Id,
            August,
            company.GetSubscriptionRate(August).Id,
            company.ServiceDescription,
            1_000m,
            23m,
            Now);
        invoice.PrepareForIssue(August, "FV/08/2026", false, "<Invoice />", Now);
        invoice.RegisterSubmission("session-test", "submission-test", Now);
        invoice.MarkIssued(
            "1010000000-20260101-000000000000-00",
            "<UPO />",
            "<Invoice />",
            Now);
        db.Companies.Add(company);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var closing = scope.ServiceProvider.GetRequiredService<IMonthClosingService>();
        var view = await closing.GetAsync(owner.Id, company.Id, August, Now);
        await closing.ConfirmRuleSetAsync(
            owner.Id,
            view!.RuleSet!.Id,
            new ConfirmCalculationRuleSetCommand(
                true,
                "synthetic:independent-review",
                new DateOnly(2026, 9, 12)),
            Now);
        await closing.SaveDeclarationAsync(
            owner.Id,
            company.Id,
            August,
            new SaveMonthDeclarationCommand(
                0m, 0m, 0m, 0m, 0m,
                true,
                true,
                "synthetic:month-ready"),
            Now);
        await closing.CloseAsync(owner.Id, company.Id, August, Now);
        return company.Id;
    }
}
