namespace Firemka.Domain.Backups;

public sealed class BackupRequest
{
    private BackupRequest()
    {
    }

    public Guid Id { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static BackupRequest CreateManual(string ownerUserId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        return new BackupRequest
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId.Trim(),
            RequestedAtUtc = nowUtc,
        };
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (CompletedAtUtc is null) CompletedAtUtc = completedAtUtc;
    }
}
