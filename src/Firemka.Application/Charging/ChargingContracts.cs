namespace Firemka.Application.Charging;

public static class ChargingCsvProfileDefaults
{
    public const char Separator = ',';
    public const string TimestampColumn = "started_at";
    public const string EnergyWhColumn = "energy [Wh]";
    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    public const string TimeZoneId = "Europe/Warsaw";
    public const string IdentityColumn = "session_id";
}

public sealed record ChargingProfileSnapshot(
    Guid Id,
    Guid CompanyId,
    int VersionNumber,
    char Separator,
    string TimestampColumn,
    string EnergyWhColumn,
    string DateFormat,
    string TimeZoneId,
    string? IdentityColumn,
    bool WhConfirmed,
    bool IsActive);

public sealed record ChargingImportLimits(long MaxBytes, int MaxRows)
{
    public static ChargingImportLimits Default => new(5 * 1024 * 1024, 50_000);
}

public sealed record ParsedChargingRow(
    int RowNumber,
    DateTimeOffset StartedAtUtc,
    DateOnly LocalMonth,
    string OriginalTimestamp,
    decimal EnergyWh,
    string? SourceIdentity,
    string NormalizedRowHash);

public sealed record ChargingCsvParseResult(
    string FileSha256,
    IReadOnlyList<ParsedChargingRow> Rows);

public sealed class ChargingCsvValidationException : Exception
{
    public ChargingCsvValidationException(string message, int? rowNumber = null)
        : base(rowNumber is null ? message : $"Wiersz {rowNumber}: {message}")
    {
        RowNumber = rowNumber;
    }

    public int? RowNumber { get; }
    public IReadOnlyList<ParsedChargingRow> AcceptedRows => [];
}

public interface IChargingCsvParser
{
    Task<ChargingCsvParseResult> ParseAsync(
        Stream content,
        ChargingProfileSnapshot profile,
        ChargingImportLimits limits,
        CancellationToken cancellationToken = default);
}

public sealed record SaveChargingProfileCommand(
    char Separator,
    string TimestampColumn,
    string EnergyWhColumn,
    string DateFormat,
    string TimeZoneId,
    string? IdentityColumn);

public sealed record ChargingImportSnapshot(
    Guid BatchId,
    int ParsedRows,
    int AddedRows,
    int SkippedRows,
    bool IsReplay);

public sealed record ChargingReportSnapshot(
    Guid ReportId,
    Guid CompanyId,
    DateOnly Month,
    Guid EnergyRatePeriodId,
    decimal GrossRatePerKwh,
    int VersionNumber,
    Guid? PreviousReportId,
    decimal TotalWh,
    decimal TotalKwh,
    decimal GrossCost,
    string TaxStatus,
    int SessionCount,
    DateTimeOffset CreatedAtUtc);

public sealed record ChargingMonthSnapshot(
    ChargingProfileSnapshot? LatestProfile,
    DateOnly Month,
    int SessionCount,
    int ImportBatchCount,
    ChargingReportSnapshot? LatestReport);

public interface IHomeChargingService
{
    Task<ChargingProfileSnapshot> SaveProfileAsync(
        string ownerUserId,
        Guid companyId,
        SaveChargingProfileCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<ChargingProfileSnapshot> ActivateProfileAsync(
        string ownerUserId,
        Guid profileId,
        bool whConfirmed,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<ChargingImportSnapshot> ImportAsync(
        string ownerUserId,
        Guid companyId,
        Guid profileId,
        Stream csv,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<ChargingReportSnapshot> GenerateReportAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<ChargingMonthSnapshot?> GetMonthAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default);

    Task<ChargingReportSnapshot?> GetReportAsync(
        string ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
