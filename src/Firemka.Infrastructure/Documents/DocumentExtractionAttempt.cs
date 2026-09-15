namespace Firemka.Infrastructure.Documents;

public enum DocumentExtractionAttemptStatus
{
    Running,
    Completed,
    Failed,
    Interrupted,
}

public sealed class DocumentExtractionAttempt
{
    public Guid Id { get; set; }

    public Guid SourceDocumentId { get; set; }

    public Guid StoredFileId { get; set; }

    public int AttemptNumber { get; set; }

    public DocumentExtractionAttemptStatus Status { get; set; }

    public string Engine { get; set; } = string.Empty;

    public string? FieldsJson { get; set; }

    public decimal? Confidence { get; set; }

    public string? FieldConfidencesJson { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }
}
