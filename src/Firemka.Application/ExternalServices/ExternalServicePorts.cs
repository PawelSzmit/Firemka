namespace Firemka.Application.ExternalServices;

public interface IKsefGateway
{
    Task<ExternalOperationResult> ExecuteAsync(
        ExternalOperationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIncomingKsefGateway
{
    Task<KsefInvoicePage> GetIncomingPageAsync(
        KsefInvoiceQuery query,
        CancellationToken cancellationToken = default);

    Task<KsefDownloadedInvoice> DownloadAsync(
        KsefInvoiceMetadata metadata,
        CancellationToken cancellationToken = default);
}

public interface IFilingExporter
{
    Task<BinaryArtifact> ExportAsync(
        FilingExportRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISubmissionGateway
{
    Task<ExternalOperationResult> SubmitAsync(
        SubmissionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDocumentExtractor
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPaymentMatcher
{
    Task<PaymentMatchResult> MatchAsync(
        PaymentMatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalOperationRequest(
    string Operation,
    string PayloadJson,
    string IdempotencyKey);

public sealed record ExternalOperationResult(
    string Status,
    string? ExternalReference,
    string? DetailsJson);

public sealed record FilingExportRequest(
    string FilingType,
    string SnapshotJson,
    string FormatVersion);

public sealed record BinaryArtifact(
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

public sealed record SubmissionRequest(
    string SubmissionType,
    ReadOnlyMemory<byte> Content,
    string IdempotencyKey);

public sealed record DocumentExtractionRequest(
    Guid StoredFileId,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content);

public sealed record DocumentExtractionResult(
    string Status,
    string FieldsJson,
    decimal? Confidence,
    string? FieldConfidencesJson = null);

public sealed record PaymentMatchRequest(
    Guid PaymentId,
    string CandidateJson);

public sealed record PaymentMatchResult(
    bool IsMatch,
    Guid? MatchedRecordId,
    string Explanation);

public enum KsefEnvironment
{
    Test,
    Demo,
    Production,
}

public sealed record KsefInvoiceQuery(
    KsefEnvironment Environment,
    DateTimeOffset InitialFromUtc,
    string? Cursor);

public sealed record KsefInvoicePage(
    IReadOnlyList<KsefInvoiceMetadata> Invoices,
    string? NextCursor,
    bool HasMore);

public sealed record KsefInvoiceMetadata(
    string KsefNumber,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateTimeOffset PermanentStorageDateUtc,
    string SellerName,
    string? SellerTaxId,
    decimal GrossAmount,
    string Currency,
    ReadOnlyMemory<byte> RawXml);

public sealed record KsefDownloadedInvoice(
    KsefInvoiceMetadata Metadata,
    ReadOnlyMemory<byte> RawXml,
    string Sha256);
