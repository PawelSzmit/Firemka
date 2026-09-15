namespace Firemka.Domain.Charging;

public sealed class ChargingImportBatch
{
    private ChargingImportBatch()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public Guid ProfileId { get; private set; }
    public string FileSha256 { get; private set; } = string.Empty;
    public int ParsedRows { get; private set; }
    public int AddedRows { get; private set; }
    public int SkippedRows { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static ChargingImportBatch Create(
        Guid companyId,
        string ownerUserId,
        Guid profileId,
        string fileSha256,
        int parsedRows,
        int addedRows,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty || profileId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikatory importu nie mogą być puste.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ChargingSession.ValidateHash(fileSha256, nameof(fileSha256));
        if (parsedRows < 0 || addedRows < 0 || addedRows > parsedRows)
        {
            throw new ArgumentOutOfRangeException(nameof(addedRows));
        }

        return new ChargingImportBatch
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            ProfileId = profileId,
            FileSha256 = fileSha256.ToUpperInvariant(),
            ParsedRows = parsedRows,
            AddedRows = addedRows,
            SkippedRows = parsedRows - addedRows,
            CompletedAtUtc = nowUtc,
        };
    }
}
