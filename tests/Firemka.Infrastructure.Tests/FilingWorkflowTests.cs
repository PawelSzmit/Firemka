using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Filings.Zus;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace Firemka.Infrastructure.Tests;

public sealed class FilingWorkflowTests : IDisposable
{
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T08:00:00Z");
    private readonly string _files = Path.Combine(Path.GetTempPath(), $"firemka-filing-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task Closed_month_creates_three_private_valid_files_once_and_preserves_manual_outcome()
    {
        await using var db = CreateDb();
        var company = await SeedClosedMonthAsync(db);
        var service = CreateService(db);
        await ConfirmProfileAsync(service, company.Id);

        var first = await service.GenerateAsync("owner-1", company.Id, September, Now);
        var replay = await service.GenerateAsync("owner-1", company.Id, September, Now.AddMinutes(1));

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(item => item.Id), replay.Select(item => item.Id));
        Assert.Equal(3, await db.FilingArtifacts.CountAsync());
        Assert.Equal(3, await db.StoredFiles.CountAsync(item =>
            item.RecordType == StoredFileRecordType.FilingArtifactVersion));
        Assert.All(first, item => Assert.Equal(FilingArtifactStatus.ApprovalRequired, item.Status));
        Assert.All(await db.StoredFiles.ToListAsync(), item => Assert.Equal(item.RecordId,
            first.Single(artifact => artifact.StoredFileId == item.Id).Id));

        var vat = first.Single(item => item.Kind == FilingArtifactKind.JpkV7M3);
        var files = new StoredFileService(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = _files,
                MaximumFileSizeBytes = 5_000_000,
            }));
        Assert.Null(await files.OpenAsync(vat.StoredFileId, "owner-1"));
        await service.ApproveAsync("owner-1", vat.Id, "Porównano z zamknięciem.", Now.AddMinutes(2));
        await using (var approvedContent = (await files.OpenAsync(vat.StoredFileId, "owner-1"))!.Content)
        {
            Assert.True(approvedContent.Length > 0);
        }
        var exported = await service.RecordExportAsync("owner-1", vat.Id, Now.AddMinutes(3));
        await service.RecordExportAsync("owner-1", vat.Id, Now.AddMinutes(4));
        await service.MarkSentAsync(
            "owner-1", vat.Id, "Ręcznie w Kliencie JPK WEB", Now.AddMinutes(5));
        var rejected = await service.RecordOutcomeAsync(
            "owner-1",
            vat.Id,
            FilingSubmissionOutcome.Rejected,
            new FilingReceiptUpload(
                "odrzucenie.xml",
                "application/xml",
                new MemoryStream("<Potwierdzenie />"u8.ToArray())),
            "Błąd walidacji w narzędziu odbiorcy",
            Now.AddMinutes(6));

        Assert.Equal(FilingArtifactStatus.Approved, exported.Status);
        Assert.Equal(FilingArtifactStatus.Rejected, rejected.Status);
        Assert.NotNull(rejected.ReceiptFileId);
        Assert.Equal(2, await db.AuditEvents.CountAsync(item => item.Action == "filing-artifact-exported"));
        Assert.Single(await db.StoredFiles.Where(item =>
            item.RecordType == StoredFileRecordType.SubmissionReceipt).ToListAsync());
    }

    [Fact]
    public async Task Closed_correction_creates_new_unapproved_artifacts_linked_to_originals()
    {
        await using var db = CreateDb();
        var company = await SeedClosedMonthAsync(db);
        var service = CreateService(db);
        await ConfirmProfileAsync(service, company.Id);
        var originals = await service.GenerateAsync("owner-1", company.Id, September, Now);
        var closing = new MonthClosingService(db, new AuditTrail(db));

        await closing.StartCorrectionAsync(
            "owner-1", company.Id, September, "Korekta kontrolna", Now.AddHours(1));
        await closing.CloseAsync("owner-1", company.Id, September, Now.AddHours(2));
        var corrections = await service.GenerateAsync(
            "owner-1", company.Id, September, Now.AddHours(3));

        Assert.Equal(3, corrections.Count);
        Assert.All(corrections, correction =>
        {
            var original = originals.Single(item => item.Kind == correction.Kind);
            Assert.Equal(2, correction.VersionNumber);
            Assert.Equal(original.Id, correction.PreviousArtifactId);
            Assert.Equal(FilingArtifactStatus.ApprovalRequired, correction.Status);
            Assert.NotEqual(original.StoredFileId, correction.StoredFileId);
        });
        Assert.Equal(6, await db.FilingArtifacts.CountAsync());
    }

    [Fact]
    public async Task Corrected_filing_profile_creates_new_unapproved_artifacts_for_the_same_closing()
    {
        await using var db = CreateDb();
        var company = await SeedClosedMonthAsync(db);
        var service = CreateService(db);
        var firstProfile = await ConfirmProfileAsync(service, company.Id);
        var originals = await service.GenerateAsync("owner-1", company.Id, September, Now);

        var correctedProfile = await service.SaveProfileAsync(
            "owner-1", company.Id, ProfileCommand() with { TaxOfficeCode = "0202" }, Now.AddMinutes(1));
        var corrected = await service.GenerateAsync(
            "owner-1", company.Id, September, Now.AddMinutes(2));

        Assert.Equal(3, corrected.Count);
        Assert.All(originals, item => Assert.Equal(firstProfile.Id, item.FilingProfileVersionId));
        Assert.All(corrected, item =>
        {
            var original = originals.Single(previous => previous.Kind == item.Kind);
            Assert.Equal(correctedProfile.Id, item.FilingProfileVersionId);
            Assert.Equal(2, item.VersionNumber);
            Assert.Equal(original.Id, item.PreviousArtifactId);
            Assert.Equal(FilingArtifactStatus.ApprovalRequired, item.Status);
            Assert.NotEqual(original.InputFingerprint, item.InputFingerprint);
        });
        Assert.Equal(6, await db.FilingArtifacts.CountAsync());

        var draftVat = await ReadArtifactXmlAsync(db, corrected.Single(item => item.Kind == FilingArtifactKind.JpkV7M3));
        var draftZus = await ReadArtifactXmlAsync(db, corrected.Single(item => item.Kind == FilingArtifactKind.ZusDraKedu227));
        XNamespace vatNamespace = JpkV7M3Generator.JpkNamespace;
        Assert.Equal("1", draftVat.Descendants(vatNamespace + "CelZlozenia").Single().Value);
        Assert.Equal("01", ZusIdentifier(draftZus));

        var sentVat = corrected.Single(item => item.Kind == FilingArtifactKind.JpkV7M3);
        var sentZus = corrected.Single(item => item.Kind == FilingArtifactKind.ZusDraKedu227);
        await service.ApproveAsync("owner-1", sentVat.Id, "synthetic:vat-reviewed", Now.AddMinutes(3));
        await service.MarkSentAsync("owner-1", sentVat.Id, "synthetic:vat-sent", Now.AddMinutes(4));
        await service.ApproveAsync("owner-1", sentZus.Id, "synthetic:zus-reviewed", Now.AddMinutes(3));
        await service.MarkSentAsync("owner-1", sentZus.Id, "synthetic:zus-sent", Now.AddMinutes(4));
        _ = await service.SaveProfileAsync(
            "owner-1", company.Id, ProfileCommand() with { TaxOfficeCode = "0203" }, Now.AddMinutes(5));
        var afterSubmission = await service.GenerateAsync(
            "owner-1", company.Id, September, Now.AddMinutes(6));

        var correctionVat = await ReadArtifactXmlAsync(db, afterSubmission.Single(item => item.Kind == FilingArtifactKind.JpkV7M3));
        var correctionZus = await ReadArtifactXmlAsync(db, afterSubmission.Single(item => item.Kind == FilingArtifactKind.ZusDraKedu227));
        Assert.Equal("2", correctionVat.Descendants(vatNamespace + "CelZlozenia").Single().Value);
        Assert.Equal("02", ZusIdentifier(correctionZus));
    }

    [Fact]
    public async Task Filing_profile_is_versioned_idempotently_and_isolated_by_owner()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyAsync(db);
        var service = CreateService(db);
        var command = ProfileCommand();

        var first = await service.SaveProfileAsync("owner-1", company.Id, command, Now);
        var replay = await service.SaveProfileAsync("owner-1", company.Id, command, Now.AddMinutes(1));
        var changed = await service.SaveProfileAsync(
            "owner-1",
            company.Id,
            command with { Email = "nowy@example.test" },
            Now.AddMinutes(2));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(2, changed.VersionNumber);
        Assert.Equal(first.Id, changed.PreviousVersionId);
        Assert.Equal(2, await db.FilingProfileVersions.CountAsync());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetProfileAsync("other-owner", company.Id));
    }

    [Fact]
    public async Task Existing_profile_still_returns_a_controlled_error_for_missing_required_text()
    {
        await using var db = CreateDb();
        var company = await SeedCompanyAsync(db);
        var service = CreateService(db);
        await ConfirmProfileAsync(service, company.Id);

        var error = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SaveProfileAsync(
            "owner-1", company.Id, ProfileCommand() with { FirstName = null! }, Now.AddMinutes(1)));

        Assert.Contains("FirstName", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Historical_unsupported_vat_profile_blocks_the_whole_filing_set()
    {
        await using var db = CreateDb();
        var company = await SeedClosedMonthAsync(db);
        var service = CreateService(db);
        await ConfirmProfileAsync(service, company.Id);
        var october = September.AddMonths(1);
        var octoberNow = DateTimeOffset.Parse("2026-11-02T08:00:00Z");
        var invoice = SalesInvoice.CreateDraft(
            company.Id, company.OwnerUserId, october, company.GetSubscriptionRate(october).Id,
            company.ServiceDescription, 1_000m, 23m, octoberNow);
        invoice.PrepareForIssue(october, "FV/10/2026", false, "<Invoice />", octoberNow);
        invoice.RegisterSubmission("session-october", "submission-october", octoberNow);
        invoice.MarkIssued(
            "1010000000-20260101-000000000001-00", "<UPO />", "<Invoice />", octoberNow);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var closing = new MonthClosingService(db, new AuditTrail(db));
        await closing.SaveDeclarationAsync(
            "owner-1", company.Id, october,
            new SaveMonthDeclarationCommand(0m, 0m, 0m, null, null, false, true, "synthetic:october"),
            octoberNow);
        await closing.CloseAsync("owner-1", company.Id, october, octoberNow);
        company.ChangeVatProfile(october, VatProfile.VatExempt, octoberNow.AddMinutes(1));
        db.Entry(company.VatProfiles.Single(item => item.ValidFromMonth == october)).State = EntityState.Added;
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync("owner-1", company.Id, october, octoberNow.AddMinutes(2)));

        Assert.Contains("profil VAT", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.FilingArtifacts.Where(item => item.Period == october).ToListAsync());
    }

    private FilingService CreateService(AppDbContext db)
        => new(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = _files,
                MaximumFileSizeBytes = 5_000_000,
            }),
            new JpkV7M3Generator(),
            new JpkPkpir3Generator(),
            new ZusDraKedu227Generator(),
            new AuditTrail(db));

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"filings-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Company> SeedCompanyAsync(AppDbContext db)
    {
        var company = Company.Register(
            "owner-1",
            "Testowa Firma",
            "1010000000",
            "Testowy adres firmy",
            September,
            "Syntetyczny Klient",
            "1234567890",
            "Adres testowego klienta",
            "Usługa testowa",
            1_000m,
            23m,
            1m,
            VehicleArrangement.None,
            Now);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<Company> SeedClosedMonthAsync(AppDbContext db)
    {
        var company = await SeedCompanyAsync(db);
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            company.OwnerUserId,
            September,
            company.GetSubscriptionRate(September).Id,
            company.ServiceDescription,
            1_000m,
            23m,
            Now);
        invoice.PrepareForIssue(September, "FV/09/2026", false, "<Invoice />", Now);
        invoice.RegisterSubmission("session-test", "submission-test", Now);
        invoice.MarkIssued(
            "1010000000-20260101-000000000000-00",
            "<UPO />",
            "<Invoice />",
            Now);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var closing = new MonthClosingService(db, new AuditTrail(db));
        var view = await closing.GetAsync("owner-1", company.Id, September, Now);
        await closing.ConfirmRuleSetAsync(
            "owner-1",
            view!.RuleSet!.Id,
            new ConfirmCalculationRuleSetCommand(
                true,
                "synthetic:independent-review",
                new DateOnly(2026, 9, 12)),
            Now);
        await closing.SaveDeclarationAsync(
            "owner-1",
            company.Id,
            September,
            new SaveMonthDeclarationCommand(
                0m, 0m, 0m, 0m, 0m,
                true,
                true,
                "synthetic:monthly-declaration"),
            Now);
        await closing.CloseAsync("owner-1", company.Id, September, Now);
        return company;
    }

    private static Task<FilingProfileSnapshot> ConfirmProfileAsync(FilingService service, Guid companyId)
        => service.SaveProfileAsync("owner-1", companyId, ProfileCommand(), Now);

    private static SaveFilingProfileCommand ProfileCommand() => new(
        "Jan",
        "Testowy",
        new DateOnly(1990, 1, 1),
        "90010112345",
        "1215",
        "0510",
        "jan@example.test",
        "synthetic:profile-checked",
        new DateOnly(2026, 9, 12));

    private async Task<XDocument> ReadArtifactXmlAsync(AppDbContext db, FilingArtifactSnapshot artifact)
    {
        var storageKey = await db.StoredFiles.AsNoTracking()
            .Where(item => item.Id == artifact.StoredFileId)
            .Select(item => item.StorageKey)
            .SingleAsync();
        return XDocument.Load(Path.Combine(_files, storageKey.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ZusIdentifier(XDocument document)
    {
        XNamespace ns = ZusDraKedu227Generator.KeduNamespace;
        return document.Descendants(ns + "I").Single().Element(ns + "p2")!.Element(ns + "p1")!.Value;
    }

    public void Dispose()
    {
        if (Directory.Exists(_files))
        {
            Directory.Delete(_files, recursive: true);
        }
    }
}
