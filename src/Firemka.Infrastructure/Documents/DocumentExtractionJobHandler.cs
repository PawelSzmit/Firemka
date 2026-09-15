using System.Text.Json;
using Firemka.Application.Auditing;
using Firemka.Application.ExternalServices;
using Firemka.Application.Jobs;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Documents;

public sealed class DocumentExtractionJobHandler(
    AppDbContext dbContext,
    StoredFileService storedFileService,
    IDocumentExtractor extractor,
    IAuditTrail auditTrail) : IBackgroundJobHandler
{
    public const string JobTypeName = "document.extract";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<DocumentExtractionJobPayload>(job.PayloadJson)
            ?? throw new InvalidDataException("Zadanie OCR ma niepoprawne dane.");
        var document = await dbContext.SourceDocuments.SingleAsync(
            item => item.Id == payload.DocumentId,
            cancellationToken);
        if (document.Status != SourceDocumentStatus.Acquired
            && document.Status != SourceDocumentStatus.ErrorToResolve)
        {
            return;
        }

        var attempt = new DocumentExtractionAttempt
        {
            Id = Guid.NewGuid(),
            SourceDocumentId = document.Id,
            StoredFileId = payload.StoredFileId,
            AttemptNumber = await dbContext.DocumentExtractionAttempts.CountAsync(
                item => item.SourceDocumentId == document.Id,
                cancellationToken) + 1,
            Status = DocumentExtractionAttemptStatus.Running,
            Engine = "local-pdftotext-tesseract",
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.DocumentExtractionAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var opened = await storedFileService.OpenAsync(
                payload.StoredFileId,
                document.OwnerUserId,
                cancellationToken)
                ?? throw new InvalidDataException("Brakuje pliku źródłowego dokumentu.");
            await using var content = opened.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var result = await extractor.ExtractAsync(
                new DocumentExtractionRequest(
                    payload.StoredFileId,
                    opened.OriginalFileName,
                    opened.MediaType,
                    buffer.ToArray()),
                cancellationToken);
            var data = JsonSerializer.Deserialize<DocumentData>(result.FieldsJson)
                ?? DocumentData.Empty;

            document.ApplyExtraction(data, result.Confidence, DateTimeOffset.UtcNow);
            attempt.Status = DocumentExtractionAttemptStatus.Completed;
            attempt.FieldsJson = result.FieldsJson;
            attempt.Confidence = result.Confidence;
            attempt.FieldConfidencesJson = result.FieldConfidencesJson;
            attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
            auditTrail.Stage(new AuditRecord(
                "document-extraction-completed",
                nameof(SourceDocument),
                document.Id.ToString(),
                "worker",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(new { result.Status, result.Confidence })));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await dbContext.Entry(document).ReloadAsync(CancellationToken.None);
            foreach (var pendingAudit in dbContext.ChangeTracker.Entries<AuditEvent>()
                         .Where(entry => entry.State == EntityState.Added))
            {
                pendingAudit.State = EntityState.Detached;
            }

            attempt.Status = DocumentExtractionAttemptStatus.Interrupted;
            attempt.Error = "Przetwarzanie przerwano; zadanie może zostać bezpiecznie wznowione.";
            attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            document.MarkProcessingError(DateTimeOffset.UtcNow);
            attempt.Status = DocumentExtractionAttemptStatus.Failed;
            attempt.Error = exception.Message.Length <= 2_000
                ? exception.Message
                : exception.Message[..2_000];
            attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
