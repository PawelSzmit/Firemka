using Firemka.Application.ExternalServices;
using Firemka.Application.Jobs;
using Firemka.Application.Documents;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Ocr;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class IncomingDocumentWorkflowTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        $"firemka-incoming-{Guid.NewGuid():N}");

    [Fact]
    public async Task Unreadable_upload_reaches_manual_review_and_can_be_confirmed()
    {
        await using var db = CreateDbContext();
        var files = CreateFileService(db);
        var queue = new BackgroundJobQueue(db);
        var audit = new AuditTrail(db);
        var service = new IncomingDocumentService(db, files, queue, audit);
        var uploaded = await service.UploadAsync(
            "owner-1",
            new IncomingDocumentUpload(
                "scan.png",
                "image/png",
                new MemoryStream(PngBytes())),
            DateTimeOffset.Parse("2026-09-10T12:00:00Z"));
        var job = await queue.TryLeaseAsync(
            "test-worker",
            DateTimeOffset.UtcNow.AddMinutes(1),
            TimeSpan.FromMinutes(2));
        Assert.NotNull(job);

        var handler = new DocumentExtractionJobHandler(
            db,
            files,
            new BlankExtractor(),
            audit);
        await handler.ExecuteAsync(job);

        var afterOcr = await db.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(SourceDocumentStatus.DataToReview, afterOcr.Status);
        Assert.Null(afterOcr.ExtractionConfidence);
        Assert.Single(await db.DocumentExtractionAttempts.AsNoTracking().ToListAsync());

        await service.ConfirmAsync(
            "owner-1",
            uploaded.DocumentId,
            new DocumentData(
                "FV/10/2026",
                "Sztuczny Dostawca",
                "1234567890",
                new DateOnly(2026, 9, 10),
                199.99m,
                "PLN"),
            DateTimeOffset.Parse("2026-09-10T12:05:00Z"));

        var confirmed = await db.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(SourceDocumentStatus.RuleToDefine, confirmed.Status);
        Assert.Equal("FV/10/2026", confirmed.InvoiceNumber);
        Assert.Contains(
            await db.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Action == "document-data-confirmed");
    }

    [Fact]
    public async Task Upload_rejects_content_that_does_not_match_the_declared_type()
    {
        await using var db = CreateDbContext();
        var service = new IncomingDocumentService(
            db,
            CreateFileService(db),
            new BackgroundJobQueue(db),
            new AuditTrail(db));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.UploadAsync(
                "owner-1",
                new IncomingDocumentUpload(
                    "fake.pdf",
                    "application/pdf",
                    new MemoryStream("not-a-pdf"u8.ToArray())),
                DateTimeOffset.UtcNow));

        Assert.Empty(await db.SourceDocuments.AsNoTracking().ToListAsync());
        Assert.Empty(await db.StoredFiles.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Failed_enqueue_removes_the_database_records_and_the_physical_file()
    {
        await using var db = CreateDbContext();
        var service = new IncomingDocumentService(
            db,
            CreateFileService(db),
            new ThrowingQueue(),
            new AuditTrail(db));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(
                "owner-1",
                new IncomingDocumentUpload(
                    "scan.png",
                    "image/png",
                    new MemoryStream(PngBytes())),
                DateTimeOffset.UtcNow));

        Assert.Empty(await db.SourceDocuments.AsNoTracking().ToListAsync());
        Assert.Empty(await db.StoredFiles.AsNoTracking().ToListAsync());
        Assert.Empty(await db.BackgroundJobs.AsNoTracking().ToListAsync());
        Assert.Empty(Directory.Exists(_rootPath)
            ? Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task Cancelled_extraction_is_marked_interrupted_and_can_be_retried()
    {
        await using var db = CreateDbContext();
        var files = CreateFileService(db);
        var queue = new BackgroundJobQueue(db);
        var audit = new AuditTrail(db);
        var service = new IncomingDocumentService(db, files, queue, audit);
        await service.UploadAsync(
            "owner-1",
            new IncomingDocumentUpload(
                "scan.png",
                "image/png",
                new MemoryStream(PngBytes())),
            DateTimeOffset.UtcNow);
        var job = await queue.TryLeaseAsync(
            "test-worker",
            DateTimeOffset.UtcNow.AddMinutes(1),
            TimeSpan.FromMinutes(2));
        Assert.NotNull(job);
        var extractor = new BlockingExtractor();
        var handler = new DocumentExtractionJobHandler(db, files, extractor, audit);
        using var cancellation = new CancellationTokenSource();

        var execution = handler.ExecuteAsync(job!, cancellation.Token);
        await extractor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        var attempt = await db.DocumentExtractionAttempts.AsNoTracking().SingleAsync();
        Assert.Equal(DocumentExtractionAttemptStatus.Interrupted, attempt.Status);
        Assert.NotNull(attempt.FinishedAtUtc);
        Assert.Equal(
            SourceDocumentStatus.Acquired,
            (await db.SourceDocuments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Inbox_pages_are_bounded_stable_and_filterable_by_status()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.Parse("2026-09-10T12:00:00Z");
        for (var index = 1; index <= 25; index++)
        {
            var document = SourceDocument.CreateManual(
                Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
                "owner-1",
                now);
            if (index <= 3)
            {
                document.AttachSource(Guid.NewGuid(), new string('A', 64), now);
                document.ApplyExtraction(DocumentData.Empty, null, now);
            }

            db.SourceDocuments.Add(document);
        }

        await db.SaveChangesAsync();
        var service = new IncomingDocumentService(
            db,
            CreateFileService(db),
            new BackgroundJobQueue(db),
            new AuditTrail(db));

        var first = await service.ListAsync(
            "owner-1",
            new IncomingDocumentListQuery(1, 10, null));
        var second = await service.ListAsync(
            "owner-1",
            new IncomingDocumentListQuery(2, 10, null));
        var third = await service.ListAsync(
            "owner-1",
            new IncomingDocumentListQuery(3, 10, null));
        var reviewOnly = await service.ListAsync(
            "owner-1",
            new IncomingDocumentListQuery(1, 10, SourceDocumentStatus.DataToReview));

        Assert.Equal(25, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(10, second.Items.Count);
        Assert.Equal(5, third.Items.Count);
        Assert.Equal(25, first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.Id).Distinct().Count());
        Assert.Equal(3, reviewOnly.TotalCount);
        Assert.All(reviewOnly.Items, item => Assert.Equal(SourceDocumentStatus.DataToReview, item.Status));
    }

    [Fact]
    public async Task Only_the_owner_can_resolve_a_source_conflict_and_the_decision_is_audited()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.Parse("2026-09-13T08:00:00Z");
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", now);
        document.MarkSourceConflict(now.AddMinutes(1));
        db.SourceDocuments.Add(document);
        await db.SaveChangesAsync();
        var service = new IncomingDocumentService(
            db,
            CreateFileService(db),
            new BackgroundJobQueue(db),
            new AuditTrail(db));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ResolveSourceConflictAsync(
                "owner-2",
                document.Id,
                "Obcy użytkownik nie może zapisać tej decyzji.",
                now.AddMinutes(2)));

        await service.ResolveSourceConflictAsync(
            "owner-1",
            document.Id,
            "Porównano oba źródła i zachowany dokument jest właściwy.",
            now.AddMinutes(3));

        var resolved = await db.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.False(resolved.HasUnresolvedSourceConflict);
        Assert.Equal(now.AddMinutes(3), resolved.SourceConflictResolvedAtUtc);
        Assert.Equal(
            "Porównano oba źródła i zachowany dokument jest właściwy.",
            resolved.SourceConflictResolution);
        var audit = await db.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.Action == "document-source-conflict-resolved");
        Assert.DoesNotContain("Porównano oba", audit.DetailsJson, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private StoredFileService CreateFileService(AppDbContext db) =>
        new(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = _rootPath,
                MaximumFileSizeBytes = 1024 * 1024,
            }));

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"firemka-incoming-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static byte[] PngBytes() =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x01];

    private sealed class BlankExtractor : IDocumentExtractor
    {
        public Task<DocumentExtractionResult> ExtractAsync(
            DocumentExtractionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DocumentExtractionResult(
                "NeedsManualReview",
                System.Text.Json.JsonSerializer.Serialize(DocumentData.Empty),
                null));
    }

    private sealed class BlockingExtractor : IDocumentExtractor
    {
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<DocumentExtractionResult> ExtractAsync(
            DocumentExtractionRequest request,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Nieosiągalne.");
        }
    }

    private sealed class ThrowingQueue : IBackgroundJobQueue
    {
        public Task<Guid> EnqueueAsync(
            BackgroundJobCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromException<Guid>(new InvalidOperationException("Kontrolowana awaria kolejki."));

        public Task<LeasedBackgroundJob?> TryLeaseAsync(
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RenewLeaseAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CompleteAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task FailAsync(
            Guid jobId,
            string workerId,
            string error,
            DateTimeOffset failedAtUtc,
            TimeSpan retryDelay,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
