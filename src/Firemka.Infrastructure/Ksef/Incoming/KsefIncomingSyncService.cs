using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.ExternalServices;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Ksef.Incoming;

public sealed class KsefIncomingSyncService(
    AppDbContext dbContext,
    IIncomingKsefGateway gateway,
    StoredFileService storedFileService,
    IAuditTrail auditTrail,
    KsefIncomingOptions options)
{
    public async Task<KsefSyncResult> SyncAsync(
        string ownerUserId,
        DateTimeOffset initialFromUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        options.ValidateForUse();

        var checkpoint = await dbContext.KsefSyncCheckpoints.SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId && item.Environment == options.Environment,
            cancellationToken);
        var cursor = checkpoint?.Cursor;
        var run = new KsefSyncRun
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Environment = options.Environment,
            Status = KsefSyncRunStatus.Running,
            StartedFromCursor = cursor,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.KsefSyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            while (true)
            {
                var page = await gateway.GetIncomingPageAsync(
                    new KsefInvoiceQuery(options.Environment, initialFromUtc, cursor),
                    cancellationToken);

                foreach (var metadata in page.Invoices)
                {
                    var downloaded = await gateway.DownloadAsync(metadata, cancellationToken);
                    var existing = await dbContext.SourceDocuments
                        .Include(item => item.SourceConflicts)
                        .SingleOrDefaultAsync(
                        item => item.OwnerUserId == ownerUserId && item.KsefNumber == metadata.KsefNumber,
                        cancellationToken);

                    if (existing is not null)
                    {
                        if (string.Equals(existing.SourceSha256, downloaded.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            run.UnchangedCount++;
                        }
                        else
                        {
                            run.ConflictCount++;
                            if (existing.IsKnownSourceConflict(downloaded.Sha256))
                            {
                                continue;
                            }

                            await using var conflictTransaction = dbContext.Database.IsRelational()
                                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                                : null;
                            StoredFileReference? conflictingStored = null;
                            try
                            {
                                await using var conflictStream = new MemoryStream(
                                    downloaded.RawXml.ToArray(),
                                    writable: false);
                                conflictingStored = await storedFileService.StoreAsync(
                                    ownerUserId,
                                    new PrivateFileUpload(
                                        $"{metadata.KsefNumber}-conflict-{existing.SourceConflicts.Count + 1}.xml",
                                        "application/xml",
                                        conflictStream),
                                    new StoredFileContext(
                                        StoredFileOrigin.KsefDownload,
                                        StoredFileRecordType.SourceDocument,
                                        existing.Id,
                                        existing.SourceConflicts.Count + 2),
                                    cancellationToken);
                                existing.MarkSourceConflict(
                                    conflictingStored.Id,
                                    downloaded.Sha256,
                                    DateTimeOffset.UtcNow);
                                var conflict = existing.SourceConflicts.Single(
                                    item => item.StoredFileId == conflictingStored.Id);
                                dbContext.Entry(conflict).State = EntityState.Added;
                                auditTrail.Stage(new AuditRecord(
                                    "ksef-source-conflict",
                                    nameof(SourceDocument),
                                    existing.Id.ToString(),
                                    ownerUserId,
                                    DateTimeOffset.UtcNow,
                                    JsonSerializer.Serialize(new
                                    {
                                        metadata.KsefNumber,
                                        conflictingStored.Id,
                                        conflictingStored.Sha256,
                                    })));
                                await dbContext.SaveChangesAsync(cancellationToken);
                                if (conflictTransaction is not null)
                                {
                                    await conflictTransaction.CommitAsync(cancellationToken);
                                }
                            }
                            catch
                            {
                                if (conflictTransaction is not null)
                                {
                                    await conflictTransaction.RollbackAsync(CancellationToken.None);
                                }

                                if (conflictingStored is not null)
                                {
                                    await storedFileService.DeletePhysicalCopyAsync(
                                        conflictingStored.Id,
                                        CancellationToken.None);
                                }

                                await RemoveFailedConflictRecordsAsync(existing, conflictingStored?.Id);
                                throw;
                            }
                        }

                        continue;
                    }

                    var document = SourceDocument.CreateKsef(
                        Guid.NewGuid(),
                        ownerUserId,
                        metadata.KsefNumber,
                        metadata.PermanentStorageDateUtc,
                        DateTimeOffset.UtcNow);
                    await using var transaction = dbContext.Database.IsRelational()
                        ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                        : null;
                    StoredFileReference? stored = null;
                    try
                    {
                        dbContext.SourceDocuments.Add(document);

                        await using var xmlStream = new MemoryStream(downloaded.RawXml.ToArray(), writable: false);
                        stored = await storedFileService.StoreAsync(
                            ownerUserId,
                            new PrivateFileUpload(
                                $"{metadata.KsefNumber}.xml",
                                "application/xml",
                                xmlStream),
                            new StoredFileContext(
                                StoredFileOrigin.KsefDownload,
                                StoredFileRecordType.SourceDocument,
                                document.Id,
                                1),
                            cancellationToken);

                        document.AttachSource(stored.Id, stored.Sha256, DateTimeOffset.UtcNow);
                        document.ApplyExtraction(
                            new DocumentData(
                                metadata.InvoiceNumber,
                                metadata.SellerName,
                                metadata.SellerTaxId,
                                metadata.IssueDate,
                                metadata.GrossAmount,
                                metadata.Currency),
                            1m,
                            DateTimeOffset.UtcNow);
                        auditTrail.Stage(new AuditRecord(
                            "ksef-invoice-imported",
                            nameof(SourceDocument),
                            document.Id.ToString(),
                            ownerUserId,
                            DateTimeOffset.UtcNow,
                            JsonSerializer.Serialize(new { metadata.KsefNumber })));
                        await dbContext.SaveChangesAsync(cancellationToken);
                        if (transaction is not null)
                        {
                            await transaction.CommitAsync(cancellationToken);
                        }

                        run.ImportedCount++;
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

                        await RemoveFailedImportRecordsAsync(document.Id, stored?.Id);
                        throw;
                    }
                }

                cursor = page.NextCursor;
                if (checkpoint is null)
                {
                    checkpoint = new KsefSyncCheckpoint
                    {
                        Id = Guid.NewGuid(),
                        OwnerUserId = ownerUserId,
                        Environment = options.Environment,
                        InitialFromUtc = initialFromUtc,
                    };
                    dbContext.KsefSyncCheckpoints.Add(checkpoint);
                }

                checkpoint.Cursor = cursor;
                checkpoint.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                if (!page.HasMore)
                {
                    break;
                }
            }

            run.Status = KsefSyncRunStatus.Completed;
            run.FinishedAtCursor = cursor;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new KsefSyncResult(
                run.ImportedCount,
                run.UnchangedCount,
                run.ConflictCount,
                cursor);
        }
        catch (Exception exception)
        {
            run.Status = KsefSyncRunStatus.Failed;
            run.Error = exception.Message.Length <= 2_000
                ? exception.Message
                : exception.Message[..2_000];
            run.FinishedAtCursor = cursor;
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task RemoveFailedImportRecordsAsync(Guid documentId, Guid? storedFileId)
    {
        if (dbContext.Database.IsRelational())
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

            return;
        }

        dbContext.AuditEvents.RemoveRange(dbContext.AuditEvents.Where(
            item => item.EntityType == nameof(SourceDocument)
                && item.EntityId == documentId.ToString()));
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

    private async Task RemoveFailedConflictRecordsAsync(
        SourceDocument document,
        Guid? storedFileId)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries().Where(entry =>
                     entry.Entity is SourceDocumentConflict conflict
                        && conflict.SourceDocumentId == document.Id
                        && conflict.StoredFileId == storedFileId
                     || entry.Entity is AuditEvent audit
                        && entry.State == EntityState.Added
                        && audit.Action == "ksef-source-conflict"
                        && audit.EntityId == document.Id.ToString()))
        {
            entry.State = EntityState.Detached;
        }

        dbContext.Entry(document).State = EntityState.Detached;
        if (storedFileId is not Guid fileId)
        {
            return;
        }

        var file = dbContext.StoredFiles.Local.FirstOrDefault(item => item.Id == fileId)
            ?? await dbContext.StoredFiles.SingleOrDefaultAsync(item => item.Id == fileId);
        if (file is null)
        {
            return;
        }

        if (dbContext.Database.IsRelational())
        {
            dbContext.Entry(file).State = EntityState.Detached;
            return;
        }

        dbContext.StoredFiles.Remove(file);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }
}
