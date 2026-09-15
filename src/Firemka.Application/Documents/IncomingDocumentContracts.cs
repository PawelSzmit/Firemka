using Firemka.Domain.Documents;

namespace Firemka.Application.Documents;

public sealed record IncomingDocumentUpload(
    string FileName,
    string MediaType,
    Stream Content);

public sealed record IncomingDocumentUploadResult(
    Guid DocumentId,
    Guid StoredFileId);

public sealed record IncomingDocumentSummary(
    Guid Id,
    SourceDocumentOrigin Origin,
    SourceDocumentStatus Status,
    string? KsefNumber,
    string? InvoiceNumber,
    string? SellerName,
    DateOnly? IssueDate,
    decimal? GrossAmount,
    string? Currency,
    bool HasSourceConflict,
    DateTimeOffset? SourceConflictDetectedAtUtc,
    DateTimeOffset? SourceConflictResolvedAtUtc,
    DateTimeOffset CreatedAtUtc)
{
    public bool HasUnresolvedSourceConflict =>
        HasSourceConflict && SourceConflictResolvedAtUtc is null;
}

public sealed record IncomingDocumentListQuery(
    int PageNumber,
    int PageSize,
    SourceDocumentStatus? Status);

public sealed record IncomingDocumentPage(
    IReadOnlyList<IncomingDocumentSummary> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (decimal)PageSize));
}

public sealed record IncomingDocumentDetails(
    IncomingDocumentSummary Summary,
    Guid? SourceFileId,
    string? SellerTaxId,
    string? SellerAddress,
    decimal? ExtractionConfidence,
    IReadOnlyDictionary<string, decimal> FieldConfidences,
    string? UnrelatedReason,
    string? SourceConflictResolution,
    IReadOnlyList<IncomingSourceConflictHistory> SourceConflicts);

public sealed record IncomingSourceConflictHistory(
    Guid Id,
    Guid? StoredFileId,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    string? Resolution);

public interface IIncomingDocumentService
{
    Task<IncomingDocumentUploadResult> UploadAsync(
        string ownerUserId,
        IncomingDocumentUpload upload,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task<IncomingDocumentPage> ListAsync(
        string ownerUserId,
        IncomingDocumentListQuery query,
        CancellationToken cancellationToken = default);

    Task<IncomingDocumentDetails?> GetAsync(
        string ownerUserId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task ConfirmAsync(
        string ownerUserId,
        Guid documentId,
        DocumentData data,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkUnrelatedAsync(
        string ownerUserId,
        Guid documentId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task ResolveSourceConflictAsync(
        string ownerUserId,
        Guid documentId,
        string resolution,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default);
}
