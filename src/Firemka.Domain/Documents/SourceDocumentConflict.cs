namespace Firemka.Domain.Documents;

public sealed class SourceDocumentConflict
{
    private SourceDocumentConflict()
    {
    }

    internal SourceDocumentConflict(
        Guid id,
        Guid sourceDocumentId,
        Guid? storedFileId,
        string? conflictingSha256,
        DateTimeOffset detectedAtUtc)
    {
        Id = id;
        SourceDocumentId = sourceDocumentId;
        StoredFileId = storedFileId;
        ConflictingSha256 = conflictingSha256;
        DetectedAtUtc = detectedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid SourceDocumentId { get; private set; }

    public Guid? StoredFileId { get; private set; }

    public string? ConflictingSha256 { get; private set; }

    public DateTimeOffset DetectedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public string? Resolution { get; private set; }

    internal void Resolve(string resolution, DateTimeOffset occurredAtUtc)
    {
        if (ResolvedAtUtc is not null)
        {
            throw new InvalidOperationException("Ten konflikt źródła został już rozwiązany.");
        }

        ResolvedAtUtc = occurredAtUtc;
        Resolution = resolution;
    }
}
