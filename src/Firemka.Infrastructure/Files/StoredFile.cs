namespace Firemka.Infrastructure.Files;

public sealed class StoredFile
{
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public StoredFileOrigin Origin { get; set; }

    public StoredFileRecordType RecordType { get; set; }

    public Guid RecordId { get; set; }

    public int RecordVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
