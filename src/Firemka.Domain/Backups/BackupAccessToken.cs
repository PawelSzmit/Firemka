namespace Firemka.Domain.Backups;

public sealed class BackupAccessToken
{
    private BackupAccessToken()
    {
    }

    public Guid Id { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public string SecretSha256 { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastUsedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public bool IsActive => RevokedAtUtc is null;

    public static BackupAccessToken Create(
        string ownerUserId,
        string prefix,
        string secretSha256,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ValidateSha256(secretSha256);
        if (prefix.Trim().Length is < 8 or > 20)
            throw new ArgumentException("Prefiks tokenu musi mieć od 8 do 20 znaków.", nameof(prefix));

        return new BackupAccessToken
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId.Trim(),
            Prefix = prefix.Trim(),
            SecretSha256 = secretSha256.ToUpperInvariant(),
            CreatedAtUtc = nowUtc,
        };
    }

    public void Touch(DateTimeOffset usedAtUtc)
    {
        if (!IsActive) throw new InvalidOperationException("Token kopii został unieważniony.");
        LastUsedAtUtc = usedAtUtc;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (RevokedAtUtc is null) RevokedAtUtc = revokedAtUtc;
    }

    private static void ValidateSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("Skrót tokenu musi być SHA-256.", nameof(value));
    }
}
