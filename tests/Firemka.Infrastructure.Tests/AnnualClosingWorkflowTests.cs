using Firemka.Application.AnnualClosing;
using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.AnnualClosing;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Pdf;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace Firemka.Infrastructure.Tests;

public sealed class AnnualClosingWorkflowTests : IDisposable
{
    private const string Owner = "annual-owner";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2027-01-10T10:00:00Z");
    private readonly string _fileRoot = Path.Combine(Path.GetTempPath(), $"firemka-annual-{Guid.NewGuid():N}");

    [Fact]
    public async Task First_business_year_requires_only_months_from_business_start_and_creates_private_files_once()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: true);
        var service = CreateAnnualService(db);
        await service.SaveDeclarationAsync(Owner, company.Id, 2026,
            Declaration(300m, 100m, 2_000m, 1_200m), Now);

        var preview = await service.GetAsync(Owner, company.Id, 2026, Now);
        Assert.Empty(preview.Blockers);
        Assert.Equal(3, preview.Months.Count);
        Assert.Equal(3_000m, preview.Preview!.Revenue);
        Assert.Equal(200m, preview.Preview.CostsAfterInventory);
        Assert.Equal(
            preview.Months.Sum(month => month.HealthIncome) - 200m,
            preview.Preview.HealthIncome);
        Assert.Equal(
            preview.Months.Sum(month => month.HealthContributionDue),
            preview.Preview.HealthContributionsDueMonthly);
        Assert.Equal(
            preview.Preview.AnnualHealthContributionDue - preview.Preview.HealthContributionsDueMonthly,
            preview.Preview.HealthSettlementDifference);
        Assert.Equal(
            preview.Preview.AnnualHealthContributionDue - 1_200m,
            preview.Preview.HealthPaymentDifference);

        var first = await service.CloseAsync(Owner, company.Id, 2026, new(true), Now);
        var replay = await service.CloseAsync(Owner, company.Id, 2026, new(true), Now.AddMinutes(1));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(3, first.Months.Count);
        Assert.NotNull(first.PdfStoredFileId);
        Assert.NotNull(first.JpkStoredFileId);
        Assert.Equal(2, await db.StoredFiles.CountAsync());
        Assert.Single(await db.AnnualArchiveRequests.ToListAsync());
        Assert.True(File.Exists(PhysicalPath(first.PdfStoredFileId!.Value, db)));
        Assert.True(File.Exists(PhysicalPath(first.JpkStoredFileId!.Value, db)));

        var approved = await service.ApproveJpkAsync(
            Owner, first.Id, "Porównano z zamknięciem roku", Now.AddMinutes(2));
        var sent = await service.MarkJpkSentAsync(
            Owner, first.Id, "Klient JPK WEB, referencja 123", Now.AddMinutes(3));
        await using var receipt = new MemoryStream("<UPO />"u8.ToArray());
        var accepted = await service.RecordJpkOutcomeAsync(
            Owner, first.Id, FilingSubmissionOutcome.Accepted,
            new FilingReceiptUpload("upo.xml", "application/xml", receipt), "UPO 123",
            Now.AddMinutes(4));

        Assert.Equal(FilingArtifactStatus.Approved, approved.JpkStatus);
        Assert.Equal(FilingArtifactStatus.Sent, sent.JpkStatus);
        Assert.Equal(FilingArtifactStatus.Accepted, accepted.JpkStatus);
        Assert.NotNull(accepted.JpkReceiptStoredFileId);
        Assert.Equal(3, await db.StoredFiles.CountAsync());
        Assert.True(File.Exists(PhysicalPath(accepted.JpkReceiptStoredFileId!.Value, db)));
    }

    [Fact]
    public async Task Missing_month_blocks_close_with_a_direct_link()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: false);
        var service = CreateAnnualService(db);
        await service.SaveDeclarationAsync(Owner, company.Id, 2026, Declaration(), Now);

        var view = await service.GetAsync(Owner, company.Id, 2026, Now);

        var blocker = Assert.Single(view.Blockers, item => item.Code == AnnualClosingBlockerCodes.MonthMissing);
        Assert.Contains("month=12", blocker.ActionUrl);
        await Assert.ThrowsAsync<AnnualClosingBlockedException>(() =>
            service.CloseAsync(Owner, company.Id, 2026, new(true), Now));
    }

    [Fact]
    public async Task Closed_year_blocks_month_correction_until_annual_correction_is_open()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: true);
        var annual = CreateAnnualService(db);
        await annual.SaveDeclarationAsync(Owner, company.Id, 2026, Declaration(), Now);
        await annual.CloseAsync(Owner, company.Id, 2026, new(true), Now);
        var monthClosing = new MonthClosingService(db, new AuditTrail(db));

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => monthClosing.StartCorrectionAsync(
            Owner, company.Id, new DateOnly(2026, 12, 1), "Korekta grudnia", Now.AddMinutes(1)));
        Assert.Contains("korektę roku", blocked.Message, StringComparison.OrdinalIgnoreCase);

        var correction = await annual.StartCorrectionAsync(
            Owner, company.Id, 2026, "Zmiana kosztu grudnia", Now.AddMinutes(2));
        var monthCorrection = await monthClosing.StartCorrectionAsync(
            Owner, company.Id, new DateOnly(2026, 12, 1), "Korekta grudnia", Now.AddMinutes(3));

        Assert.Equal(AnnualClosingStatus.OpenCorrection, correction.Status);
        Assert.Equal(Firemka.Domain.Calculations.MonthSettlementStatus.OpenCorrection, monthCorrection.Status);
    }

    [Fact]
    public async Task Only_latest_annual_version_can_be_approved()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: true);
        var annual = CreateAnnualService(db);
        await annual.SaveDeclarationAsync(Owner, company.Id, 2026, Declaration(), Now);
        var first = await annual.CloseAsync(Owner, company.Id, 2026, new(true), Now);
        await annual.StartCorrectionAsync(Owner, company.Id, 2026, "Pierwsza korekta", Now.AddMinutes(1));
        db.ChangeTracker.Clear();

        var obsolete = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            annual.ApproveJpkAsync(Owner, first.Id, "Nieaktualna wersja", Now.AddMinutes(2)));

        Assert.Contains("najnowszej wersji", obsolete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sent_historical_annual_jpk_can_receive_its_later_outcome()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: true);
        var annual = CreateAnnualService(db);
        await annual.SaveDeclarationAsync(Owner, company.Id, 2026, Declaration(), Now);
        var first = await annual.CloseAsync(Owner, company.Id, 2026, new(true), Now);
        await annual.ApproveJpkAsync(Owner, first.Id, "Sprawdzono v1", Now.AddMinutes(1));
        await annual.MarkJpkSentAsync(Owner, first.Id, "Wysłano v1", Now.AddMinutes(2));
        await annual.StartCorrectionAsync(Owner, company.Id, 2026, "Korekta po wysyłce", Now.AddMinutes(3));
        db.ChangeTracker.Clear();
        await using var receipt = new MemoryStream("<UPO />"u8.ToArray());
        var historicalOutcome = await annual.RecordJpkOutcomeAsync(
            Owner, first.Id, FilingSubmissionOutcome.Accepted,
            new FilingReceiptUpload("upo.xml", "application/xml", receipt), "UPO v1",
            Now.AddMinutes(4));

        Assert.Equal(FilingArtifactStatus.Accepted, historicalOutcome.JpkStatus);
    }

    [Fact]
    public async Task Annual_jpk_uses_the_recognized_service_month_for_an_invoice_issued_next_year()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyWithMonthsAsync(db, closeAllMonths: true, lateDecemberInvoice: true);
        var annual = CreateAnnualService(db);
        await annual.SaveDeclarationAsync(Owner, company.Id, 2026, Declaration(), Now);

        var closing = await annual.CloseAsync(Owner, company.Id, 2026, new(true), Now);

        var xmlPath = PhysicalPath(closing.JpkStoredFileId!.Value, db);
        var xml = XDocument.Load(xmlPath);
        var decemberRow = xml.Descendants()
            .Single(item => item.Name.LocalName == "PKPIRWiersz"
                && item.Elements().Any(value => value.Name.LocalName == "K_3A"
                    && value.Value == "FV/202612"));
        Assert.Equal("2026-12-01", decemberRow.Elements().Single(item => item.Name.LocalName == "K_2").Value);
    }

    private async Task<Company> SeedCompanyWithMonthsAsync(
        AppDbContext db, bool closeAllMonths, bool lateDecemberInvoice = false)
    {
        var october = new DateOnly(2026, 10, 1);
        var company = Company.Register(
            Owner, "Roczna Firma Żółta", "1010000000", "ul. Źródlana 1, Łódź", october,
            "Syntetyczny Klient", "1234567890", "Adres klienta", "Usługa testowa",
            1_000m, 23m, 1m, VehicleArrangement.None, Now);
        db.Companies.Add(company);
        foreach (var month in Enumerable.Range(10, 3).Select(number => new DateOnly(2026, number, 1)))
        {
            var invoice = SalesInvoice.CreateDraft(company.Id, Owner, month,
                company.GetSubscriptionRate(month).Id, company.ServiceDescription, 1_000m, 23m, Now);
            var issueDate = lateDecemberInvoice && month.Month == 12
                ? new DateOnly(2027, 1, 2)
                : month;
            invoice.PrepareForIssue(issueDate, $"FV/{month:yyyyMM}", false, "<Invoice />", Now);
            invoice.RegisterSubmission($"session-{month:MM}", $"submission-{month:MM}", Now);
            invoice.MarkIssued($"1010000000-2026{month:MM}01-000000000000-00", "<UPO />", "<Invoice />", Now);
            db.SalesInvoices.Add(invoice);
            if (lateDecemberInvoice && month.Month == 12)
            {
                db.MonthTaxAdjustments.Add(MonthTaxAdjustment.Create(
                    company.Id, Owner, month, MonthAdjustmentKind.SalesRecognition, 0m,
                    "rozpoznanie sprzedaży w miesiącu usługi", "synthetic:late-sale-recognition",
                    null, invoice.Id, Now));
            }
        }
        db.FilingProfileVersions.Add(FilingProfileVersion.Create(
            company.Id, Owner, 1, null, "Paweł", "Żmuda", new DateOnly(1990, 1, 1),
            "90010112345", "1215", "0510", "pawel@example.test", "synthetic:profile",
            new DateOnly(2026, 12, 31), Now, new DateOnly(2027, 1, 10)));
        await db.SaveChangesAsync();

        var closing = new MonthClosingService(db, new AuditTrail(db));
        var first = await closing.GetAsync(Owner, company.Id, october, Now);
        await closing.ConfirmRuleSetAsync(Owner, first!.RuleSet!.Id,
            new ConfirmCalculationRuleSetCommand(true, "synthetic:rules", new DateOnly(2026, 12, 31)), Now);
        var monthsToClose = closeAllMonths ? 3 : 2;
        for (var offset = 0; offset < monthsToClose; offset++)
        {
            var month = october.AddMonths(offset);
            await closing.SaveDeclarationAsync(Owner, company.Id, month,
                new SaveMonthDeclarationCommand(0m, 0m, 0m,
                    offset == 0 ? 0m : null, offset == 0 ? 0m : null,
                    offset == 0, true, "synthetic:month"), Now);
            await closing.CloseAsync(Owner, company.Id, month, Now);
        }
        return company;
    }

    private AnnualClosingService CreateAnnualService(AppDbContext db)
    {
        var store = new PrivateFileStore(new PrivateFileStoreOptions
        {
            RootPath = _fileRoot,
            MaximumFileSizeBytes = 5_000_000,
        });
        return new AnnualClosingService(
            db, store, new JpkPkpir3Generator(), new AnnualReportPdfGenerator(),
            new MonthClosingService(db, new AuditTrail(db)), new AnnualArchiveRequestQueue(db), new AuditTrail(db));
    }

    private static SaveAnnualDeclarationCommand Declaration(
        decimal opening = 0m, decimal closing = 0m, decimal pitPaid = 0m, decimal healthPaid = 0m)
        => new(opening, closing, pitPaid, healthPaid, true, "synthetic:annual-review",
            new DateOnly(2027, 1, 10));

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"annual-{Guid.NewGuid():N}").Options);

    private string PhysicalPath(Guid fileId, AppDbContext db)
    {
        var storageKey = db.StoredFiles.Single(item => item.Id == fileId).StorageKey;
        return Path.Combine(_fileRoot, storageKey.Replace('/', Path.DirectorySeparatorChar));
    }

    public void Dispose()
    {
        if (Directory.Exists(_fileRoot)) Directory.Delete(_fileRoot, recursive: true);
        GC.SuppressFinalize(this);
    }
}
