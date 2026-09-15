namespace Firemka.Domain.Charging;

public sealed class ChargingCsvProfile
{
    private ChargingCsvProfile()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public Guid? PreviousProfileId { get; private set; }
    public char Separator { get; private set; }
    public string TimestampColumn { get; private set; } = string.Empty;
    public string EnergyWhColumn { get; private set; } = string.Empty;
    public string DateFormat { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public string? IdentityColumn { get; private set; }
    public bool WhConfirmed { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public static ChargingCsvProfile Create(
        Guid companyId,
        string ownerUserId,
        char separator,
        string timestampColumn,
        string energyWhColumn,
        string dateFormat,
        string timeZoneId,
        string? identityColumn,
        DateTimeOffset nowUtc)
        => Create(
            Guid.NewGuid(),
            companyId,
            ownerUserId,
            separator,
            timestampColumn,
            energyWhColumn,
            dateFormat,
            timeZoneId,
            identityColumn,
            1,
            null,
            nowUtc);

    public void Activate(bool whConfirmed, DateTimeOffset nowUtc)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Profil jest już aktywny.");
        }

        if (!whConfirmed)
        {
            throw new InvalidOperationException("Przed aktywacją trzeba jawnie potwierdzić jednostkę Wh.");
        }

        WhConfirmed = true;
        IsActive = true;
        ActivatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public ChargingCsvProfile CreateRevision(
        char separator,
        string timestampColumn,
        string energyWhColumn,
        string dateFormat,
        string timeZoneId,
        string? identityColumn,
        DateTimeOffset nowUtc)
        => Create(
            Guid.NewGuid(),
            CompanyId,
            OwnerUserId,
            separator,
            timestampColumn,
            energyWhColumn,
            dateFormat,
            timeZoneId,
            identityColumn,
            checked(VersionNumber + 1),
            Id,
            nowUtc);

    public void Deactivate(DateTimeOffset nowUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static ChargingCsvProfile Create(
        Guid id,
        Guid companyId,
        string ownerUserId,
        char separator,
        string timestampColumn,
        string energyWhColumn,
        string dateFormat,
        string timeZoneId,
        string? identityColumn,
        int versionNumber,
        Guid? previousProfileId,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator firmy nie może być pusty.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(timestampColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(energyWhColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(dateFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (separator is '\0' or '\r' or '\n' or '"')
        {
            throw new ArgumentException("Separator CSV jest niedozwolony.", nameof(separator));
        }

        var timestamp = timestampColumn.Trim();
        var energy = energyWhColumn.Trim();
        var identity = string.IsNullOrWhiteSpace(identityColumn) ? null : identityColumn.Trim();
        var columns = new[] { timestamp, energy, identity }
            .Where(item => item is not null)
            .Cast<string>()
            .ToArray();
        if (columns.Distinct(StringComparer.Ordinal).Count() != columns.Length)
        {
            throw new ArgumentException("Każda kolumna profilu CSV musi mieć inną nazwę.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());

        return new ChargingCsvProfile
        {
            Id = id,
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            Separator = separator,
            TimestampColumn = timestamp,
            EnergyWhColumn = energy,
            DateFormat = dateFormat.Trim(),
            TimeZoneId = timeZoneId.Trim(),
            IdentityColumn = identity,
            VersionNumber = versionNumber,
            PreviousProfileId = previousProfileId,
            CreatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
    }
}
