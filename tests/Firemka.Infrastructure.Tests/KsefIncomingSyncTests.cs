using System.Security.Cryptography;
using Firemka.Application.ExternalServices;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Ksef.Incoming;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class KsefIncomingSyncTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        $"firemka-ksef-{Guid.NewGuid():N}");

    [Fact]
    public async Task Repeating_the_same_sync_does_not_duplicate_an_invoice()
    {
        await using var db = CreateDbContext();
        var gateway = new FakeKsefGateway([Page("cursor-1", Invoice("KSEF-1", "FV/1"))]);
        var service = CreateService(db, gateway);

        var first = await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        gateway.Reset([Page("cursor-1", Invoice("KSEF-1", "FV/1"))]);
        var second = await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        Assert.Equal(1, first.ImportedCount);
        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(1, second.UnchangedCount);
        Assert.Single(await db.SourceDocuments.AsNoTracking().ToListAsync());
        Assert.Single(await db.StoredFiles.AsNoTracking().ToListAsync());
        Assert.Empty(await db.SourceDocumentConflicts.AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.KsefSyncRuns.CountAsync());
    }

    [Fact]
    public async Task Interrupted_page_is_replayed_safely_and_finishes_on_the_next_run()
    {
        await using var db = CreateDbContext();
        var firstInvoice = Invoice("KSEF-1", "FV/1");
        var secondInvoice = Invoice("KSEF-2", "FV/2");
        var gateway = new FakeKsefGateway([Page("cursor-after-page", firstInvoice, secondInvoice)])
        {
            FailDownloadOnceFor = "KSEF-2",
        };
        var service = CreateService(db, gateway);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z")));

        Assert.Single(await db.SourceDocuments.AsNoTracking().ToListAsync());
        Assert.Null(await db.KsefSyncCheckpoints.AsNoTracking().SingleOrDefaultAsync());

        gateway.Reset([Page("cursor-after-page", firstInvoice, secondInvoice)]);
        var resumed = await service.SyncAsync(
            "owner-1",
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        Assert.Equal(1, resumed.ImportedCount);
        Assert.Equal(1, resumed.UnchangedCount);
        Assert.Equal(2, await db.SourceDocuments.CountAsync());
        Assert.Equal("cursor-after-page", (await db.KsefSyncCheckpoints.SingleAsync()).Cursor);
        Assert.Equal(2, await db.KsefSyncRuns.CountAsync());
        Assert.Equal(KsefSyncRunStatus.Failed, (await db.KsefSyncRuns.OrderBy(item => item.StartedAtUtc).FirstAsync()).Status);
    }

    [Fact]
    public async Task Changed_xml_for_an_existing_ksef_number_is_detected_without_overwriting_the_source()
    {
        await using var db = CreateDbContext();
        var original = Invoice("KSEF-1", "FV/1", "<Invoice>original</Invoice>");
        var changed = Invoice("KSEF-1", "FV/1", "<Invoice>changed</Invoice>");
        var gateway = new FakeKsefGateway([Page("cursor-1", original)]);
        var service = CreateService(db, gateway);
        await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        var originalHash = (await db.SourceDocuments.AsNoTracking().SingleAsync()).SourceSha256;

        gateway.Reset([Page("cursor-2", changed)]);
        var result = await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        var document = await db.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(1, result.ConflictCount);
        Assert.Equal(originalHash, document.SourceSha256);
        Assert.Equal(Firemka.Domain.Documents.SourceDocumentStatus.ErrorToResolve, document.Status);
        Assert.Equal(2, await db.StoredFiles.AsNoTracking().CountAsync());
        Assert.Single(await db.SourceDocumentConflicts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Resolved_ksef_difference_stays_resolved_until_a_different_source_appears()
    {
        await using var db = CreateDbContext();
        var original = Invoice("KSEF-1", "FV/1", "<Invoice>original</Invoice>");
        var firstDifference = Invoice("KSEF-1", "FV/1", "<Invoice>first difference</Invoice>");
        var secondDifference = Invoice("KSEF-1", "FV/1", "<Invoice>second difference</Invoice>");
        var gateway = new FakeKsefGateway([Page("cursor-1", original)]);
        var service = CreateService(db, gateway);
        await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        gateway.Reset([Page("cursor-2", firstDifference)]);
        await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        Assert.Equal(2, await db.StoredFiles.CountAsync());
        var document = await db.SourceDocuments.SingleAsync();
        document.ResolveSourceConflict(
            "Porównano źródła; zachowana wersja jest prawidłowa.",
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"));
        await db.SaveChangesAsync();

        gateway.Reset([Page("cursor-3", firstDifference)]);
        await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        Assert.False((await db.SourceDocuments.AsNoTracking().SingleAsync()).HasUnresolvedSourceConflict);
        Assert.Equal(2, await db.StoredFiles.CountAsync());

        gateway.Reset([Page("cursor-4", secondDifference)]);
        await service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        Assert.True((await db.SourceDocuments.AsNoTracking().SingleAsync()).HasUnresolvedSourceConflict);
        Assert.Equal(3, await db.StoredFiles.CountAsync());
        Assert.Equal(2, await db.SourceDocumentConflicts.CountAsync());
    }

    [Fact]
    public async Task Failure_after_saving_xml_removes_the_document_and_physical_file()
    {
        await using var db = CreateDbContext();
        var gateway = new FakeKsefGateway([Page("cursor-1", Invoice("KSEF-1", "FV/1"))]);
        var service = CreateService(db, gateway, new ThrowingAuditTrail());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SyncAsync("owner-1", DateTimeOffset.Parse("2026-09-01T00:00:00Z")));

        Assert.Empty(await db.SourceDocuments.AsNoTracking().ToListAsync());
        Assert.Empty(await db.StoredFiles.AsNoTracking().ToListAsync());
        Assert.Empty(Directory.Exists(_rootPath)
            ? Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories)
            : []);
        Assert.Equal(KsefSyncRunStatus.Failed, (await db.KsefSyncRuns.SingleAsync()).Status);
    }

    [Fact]
    public async Task Failure_after_saving_a_conflicting_source_removes_only_the_new_file_and_history_entry()
    {
        await using var db = CreateDbContext();
        var original = Invoice("KSEF-1", "FV/1", "<Invoice>original</Invoice>");
        var changed = Invoice("KSEF-1", "FV/1", "<Invoice>changed</Invoice>");
        var gateway = new FakeKsefGateway([Page("cursor-1", original)]);
        await CreateService(db, gateway).SyncAsync(
            "owner-1",
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        gateway.Reset([Page("cursor-2", changed)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db, gateway, new ThrowingAuditTrail()).SyncAsync(
                "owner-1",
                DateTimeOffset.Parse("2026-09-01T00:00:00Z")));

        var document = await db.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.False(document.HasSourceConflict);
        Assert.Empty(await db.SourceDocumentConflicts.AsNoTracking().ToListAsync());
        Assert.Single(await db.StoredFiles.AsNoTracking().ToListAsync());
        Assert.Single(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private KsefIncomingSyncService CreateService(
        AppDbContext db,
        IIncomingKsefGateway gateway,
        Firemka.Application.Auditing.IAuditTrail? auditTrail = null)
    {
        var store = new StoredFileService(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = _rootPath,
                MaximumFileSizeBytes = 1024 * 1024,
            }));
        return new KsefIncomingSyncService(
            db,
            gateway,
            store,
            auditTrail ?? new AuditTrail(db),
            new KsefIncomingOptions
            {
                Enabled = true,
                Environment = KsefEnvironment.Test,
            });
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"firemka-ksef-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static KsefInvoicePage Page(string cursor, params KsefInvoiceMetadata[] invoices) =>
        new(invoices, cursor, false);

    private static KsefInvoiceMetadata Invoice(
        string ksefNumber,
        string invoiceNumber,
        string xml = "<Invoice>synthetic</Invoice>") =>
        new(
            ksefNumber,
            invoiceNumber,
            new DateOnly(2026, 9, 9),
            DateTimeOffset.Parse("2026-09-09T10:00:00Z"),
            "Sztuczny Dostawca",
            "1234567890",
            123.45m,
            "PLN",
            System.Text.Encoding.UTF8.GetBytes(xml));

    private sealed class FakeKsefGateway(IEnumerable<KsefInvoicePage> pages) : IIncomingKsefGateway
    {
        private Queue<KsefInvoicePage> _pages = new(pages);
        private readonly HashSet<string> _failed = [];

        public string? FailDownloadOnceFor { get; init; }

        public Task<KsefInvoicePage> GetIncomingPageAsync(
            KsefInvoiceQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_pages.Count > 0 ? _pages.Dequeue() : new KsefInvoicePage([], query.Cursor, false));
        }

        public Task<KsefDownloadedInvoice> DownloadAsync(
            KsefInvoiceMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (metadata.KsefNumber == FailDownloadOnceFor && _failed.Add(metadata.KsefNumber))
            {
                throw new HttpRequestException("Kontrolowane przerwanie pobierania.");
            }

            return Task.FromResult(new KsefDownloadedInvoice(
                metadata,
                metadata.RawXml,
                Convert.ToHexString(SHA256.HashData(metadata.RawXml.Span))));
        }

        public void Reset(IEnumerable<KsefInvoicePage> pages)
        {
            _pages = new Queue<KsefInvoicePage>(pages);
        }
    }

    private sealed class ThrowingAuditTrail : Firemka.Application.Auditing.IAuditTrail
    {
        public Guid Stage(Firemka.Application.Auditing.AuditRecord record) =>
            throw new InvalidOperationException("Kontrolowana awaria audytu.");
    }
}
