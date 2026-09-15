using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.Documents;
using Firemka.Application.Jobs;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Documents;

public sealed class IncomingDocumentService(
    AppDbContext dbContext,
    StoredFileService storedFileService,
    IBackgroundJobQueue backgroundJobQueue,
    IAuditTrail auditTrail) : IIncomingDocumentService
{
    public async Task<IncomingDocumentUploadResult> UploadAsync(
        string ownerUserId,
        IncomingDocumentUpload upload,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(upload);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var document = SourceDocument.CreateManual(Guid.NewGuid(), ownerUserId, occurredAtUtc);
        StoredFileReference? stored = null;
        try
        {
            dbContext.SourceDocuments.Add(document);
            stored = await storedFileService.StoreAsync(
                ownerUserId,
                new PrivateFileUpload(upload.FileName, upload.MediaType, upload.Content),
                new StoredFileContext(
                    StoredFileOrigin.ManualUpload,
                    StoredFileRecordType.SourceDocument,
                    document.Id,
                    1),
                cancellationToken);
            document.AttachSource(stored.Id, stored.Sha256, occurredAtUtc);
            auditTrail.Stage(new AuditRecord(
                "document-uploaded",
                nameof(SourceDocument),
                document.Id.ToString(),
                ownerUserId,
                occurredAtUtc,
                JsonSerializer.Serialize(new { stored.Id, stored.MediaType, stored.SizeBytes })));
            await dbContext.SaveChangesAsync(cancellationToken);

            await backgroundJobQueue.EnqueueAsync(
                new BackgroundJobCommand(
                    DocumentExtractionJobHandler.JobTypeName,
                    JsonSerializer.Serialize(new DocumentExtractionJobPayload(document.Id, stored.Id)),
                    $"document.extract:{document.Id}:1",
                    occurredAtUtc),
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new IncomingDocumentUploadResult(document.Id, stored.Id);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            if (stored is not null)
            {
                await storedFileService.DeletePhysicalCopyAsync(stored.Id, CancellationToken.None);
            }

            await RemoveFailedUploadRecordsAsync(document.Id, stored?.Id);
            throw;
        }
    }

    public async Task<IncomingDocumentPage> ListAsync(
        string ownerUserId,
        IncomingDocumentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var source = dbContext.SourceDocuments.AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId);
        if (query.Status is SourceDocumentStatus status)
        {
            source = source.Where(item => item.Status == status);
        }

        var totalCount = await source.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (decimal)pageSize));
        pageNumber = Math.Min(pageNumber, totalPages);
        var documents = await source
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new IncomingDocumentPage(
            documents.Select(ToSummary).ToArray(),
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task<IncomingDocumentDetails?> GetAsync(
        string ownerUserId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.SourceDocuments.AsNoTracking()
            .Include(item => item.SourceConflicts)
            .SingleOrDefaultAsync(
            item => item.Id == documentId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        var confidenceJson = await dbContext.DocumentExtractionAttempts.AsNoTracking()
            .Where(item => item.SourceDocumentId == document.Id)
            .OrderByDescending(item => item.AttemptNumber)
            .Select(item => item.FieldConfidencesJson)
            .FirstOrDefaultAsync(cancellationToken);
        var fieldConfidences = string.IsNullOrWhiteSpace(confidenceJson)
            ? new Dictionary<string, decimal>()
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>(confidenceJson)
                ?? new Dictionary<string, decimal>();
        return new IncomingDocumentDetails(
                ToSummary(document),
                document.SourceFileId,
                document.SellerTaxId,
                document.SellerAddress,
                document.ExtractionConfidence,
                fieldConfidences,
                document.UnrelatedReason,
                document.SourceConflictResolution,
                document.SourceConflicts
                    .OrderByDescending(item => item.DetectedAtUtc)
                    .ThenByDescending(item => item.Id)
                    .Select(item => new IncomingSourceConflictHistory(
                        item.Id,
                        item.StoredFileId,
                        item.DetectedAtUtc,
                        item.ResolvedAtUtc,
                        item.Resolution))
                    .ToArray());
    }

    public async Task ConfirmAsync(
        string ownerUserId,
        Guid documentId,
        DocumentData data,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var document = await OwnedDocumentAsync(ownerUserId, documentId, cancellationToken);
        document.ConfirmData(data, occurredAtUtc);
        auditTrail.Stage(new AuditRecord(
            "document-data-confirmed",
            nameof(SourceDocument),
            document.Id.ToString(),
            ownerUserId,
            occurredAtUtc,
            JsonSerializer.Serialize(new
            {
                document.DataRevisionNumber,
                correctedManually = true,
            })));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkUnrelatedAsync(
        string ownerUserId,
        Guid documentId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var document = await OwnedDocumentAsync(ownerUserId, documentId, cancellationToken);
        document.MarkUnrelated(reason, occurredAtUtc);
        auditTrail.Stage(new AuditRecord(
            "document-marked-unrelated",
            nameof(SourceDocument),
            document.Id.ToString(),
            ownerUserId,
            occurredAtUtc,
            JsonSerializer.Serialize(new { reason = "recorded" })));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveSourceConflictAsync(
        string ownerUserId,
        Guid documentId,
        string resolution,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var document = await OwnedDocumentAsync(ownerUserId, documentId, cancellationToken);
        var conflictCount = document.SourceConflicts.Count;
        document.ResolveSourceConflict(resolution, occurredAtUtc);
        if (document.SourceConflicts.Count > conflictCount)
        {
            dbContext.Entry(document.SourceConflicts.Last()).State = EntityState.Added;
        }
        auditTrail.Stage(new AuditRecord(
            "document-source-conflict-resolved",
            nameof(SourceDocument),
            document.Id.ToString(),
            ownerUserId,
            occurredAtUtc,
            JsonSerializer.Serialize(new { resolution = "recorded" })));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException(
                "Dokument zmienił się w tym samym czasie. Odśwież stronę i sprawdź najnowszy konflikt.",
                exception);
        }
    }

    private async Task<SourceDocument> OwnedDocumentAsync(
        string ownerUserId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        await dbContext.SourceDocuments
            .Include(item => item.SourceConflicts)
            .SingleOrDefaultAsync(
            item => item.Id == documentId && item.OwnerUserId == ownerUserId,
            cancellationToken)
        ?? throw new KeyNotFoundException("Nie znaleziono dokumentu.");

    private async Task RemoveFailedUploadRecordsAsync(Guid documentId, Guid? storedFileId)
    {
        if (dbContext.Database.IsRelational())
        {
            DetachFailedUploadEntries(documentId, storedFileId);
            return;
        }

        dbContext.AuditEvents.RemoveRange(dbContext.AuditEvents.Where(
            item => item.EntityType == nameof(SourceDocument)
                && item.EntityId == documentId.ToString()));
        dbContext.BackgroundJobs.RemoveRange(dbContext.BackgroundJobs.Where(
            item => item.IdempotencyKey == $"document.extract:{documentId}:1"));
        var document = dbContext.SourceDocuments.Local.FirstOrDefault(item => item.Id == documentId)
            ?? await dbContext.SourceDocuments.SingleOrDefaultAsync(item => item.Id == documentId);
        if (document is not null)
        {
            dbContext.SourceDocuments.Remove(document);
        }

        if (storedFileId is Guid fileId)
        {
            var file = dbContext.StoredFiles.Local.FirstOrDefault(item => item.Id == fileId)
                ?? await dbContext.StoredFiles.SingleOrDefaultAsync(item => item.Id == fileId);
            if (file is not null)
            {
                dbContext.StoredFiles.Remove(file);
            }
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private void DetachFailedUploadEntries(Guid documentId, Guid? storedFileId)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries().Where(entry =>
                     entry.Entity is SourceDocument source && source.Id == documentId
                     || entry.Entity is StoredFile file && file.Id == storedFileId
                     || entry.Entity is AuditEvent audit
                        && audit.EntityType == nameof(SourceDocument)
                        && audit.EntityId == documentId.ToString()))
        {
            entry.State = EntityState.Detached;
        }
    }

    private static IncomingDocumentSummary ToSummary(SourceDocument item) =>
        new(
            item.Id,
            item.Origin,
            item.Status,
            item.KsefNumber,
            item.InvoiceNumber,
            item.SellerName,
            item.IssueDate,
            item.GrossAmount,
            item.Currency,
            item.HasSourceConflict,
            item.SourceConflictDetectedAtUtc,
            item.SourceConflictResolvedAtUtc,
            item.CreatedAtUtc);
}

public sealed record DocumentExtractionJobPayload(Guid DocumentId, Guid StoredFileId);
