namespace Firemka.Domain.Charging;

public sealed class ChargingSession
{
    private ChargingSession()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public Guid FirstImportBatchId { get; private set; }
    public DateOnly LocalMonth { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public string OriginalTimestamp { get; private set; } = string.Empty;
    public decimal EnergyWh { get; private set; }
    public string NormalizedRowHash { get; private set; } = string.Empty;
    public string? SourceIdentity { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static ChargingSession Create(
        Guid companyId,
        string ownerUserId,
        Guid firstImportBatchId,
        DateOnly localMonth,
        DateTimeOffset startedAtUtc,
        string originalTimestamp,
        decimal energyWh,
        string normalizedRowHash,
        string? sourceIdentity,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty || firstImportBatchId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator firmy nie może być pusty.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalTimestamp);
        if (localMonth.Day != 1)
        {
            throw new ArgumentException("Miesiąc sesji musi zaczynać się pierwszego dnia.", nameof(localMonth));
        }

        if (energyWh <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energyWh), "Energia musi być większa od zera.");
        }

        ValidateHash(normalizedRowHash, nameof(normalizedRowHash));
        return new ChargingSession
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            FirstImportBatchId = firstImportBatchId,
            LocalMonth = localMonth,
            StartedAtUtc = startedAtUtc.ToUniversalTime(),
            OriginalTimestamp = originalTimestamp.Trim(),
            EnergyWh = energyWh,
            NormalizedRowHash = normalizedRowHash.ToUpperInvariant(),
            SourceIdentity = string.IsNullOrWhiteSpace(sourceIdentity) ? null : sourceIdentity.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }

    internal static void ValidateHash(string hash, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Skrót SHA-256 musi mieć 64 znaki szesnastkowe.", parameterName);
        }
    }
}
