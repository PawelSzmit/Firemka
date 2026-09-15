namespace Firemka.Domain.Backups;

public sealed class BackupState
{
    private BackupState()
    {
    }

    public Guid Id { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateTimeOffset ConfiguredAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastSuccessfulAtUtc { get; private set; }
    public string? LastFileName { get; private set; }
    public string? LastManifestSha256 { get; private set; }
    public int? LastFormatVersion { get; private set; }
    public string? LastFailureCode { get; private set; }

    public static BackupState Create(string ownerUserId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        return new BackupState
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId.Trim(),
            ConfiguredAtUtc = nowUtc,
        };
    }

    public void RecordSuccess(
        string fileName,
        string manifestSha256,
        int formatVersion,
        DateTimeOffset verifiedAtUtc)
    {
        ValidateFileName(fileName);
        ValidateSha256(manifestSha256);
        if (formatVersion <= 0) throw new ArgumentOutOfRangeException(nameof(formatVersion));
        LastAttemptAtUtc = verifiedAtUtc;
        LastSuccessfulAtUtc = verifiedAtUtc;
        LastFileName = fileName;
        LastManifestSha256 = manifestSha256.ToUpperInvariant();
        LastFormatVersion = formatVersion;
        LastFailureCode = null;
    }

    public void RecordFailure(string failureCode, DateTimeOffset attemptedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        var normalized = failureCode.Trim();
        if (normalized.Length > 120)
            throw new ArgumentException("Kod błędu kopii jest zbyt długi.", nameof(failureCode));
        LastAttemptAtUtc = attemptedAtUtc;
        LastFailureCode = normalized;
    }

    public bool IsOverdue(DateTimeOffset nowUtc, TimeSpan maximumAge, bool hasActiveToken)
    {
        if (!hasActiveToken) return false;
        if (maximumAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAge));
        return nowUtc - (LastSuccessfulAtUtc ?? ConfiguredAtUtc) > maximumAge;
    }

    private static void ValidateFileName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal)
            || !value.EndsWith(".fmbak", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Nazwa kopii musi być bezpiecznym plikiem .fmbak.", nameof(value));
    }

    private static void ValidateSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("Skrót manifestu musi być SHA-256.", nameof(value));
    }
}
