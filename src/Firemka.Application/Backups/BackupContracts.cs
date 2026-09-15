namespace Firemka.Application.Backups;

public sealed record BackupOverview(
    bool IsConfigured,
    string? ActiveTokenPrefix,
    DateTimeOffset? LastSuccessfulAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    string? LastFileName,
    string? LastFailureCode,
    bool IsOverdue,
    int PendingManualRequests,
    IReadOnlyList<int> PendingAnnualYears);

public sealed record IssuedBackupToken(string RawToken, string Prefix);

public sealed record BackupAuthorization(Guid TokenId, string OwnerUserId);

public sealed record AnnualBackupPlan(Guid RequestId, int TaxYear);

public sealed record BackupPlan(
    bool DailyRequired,
    Guid? ManualRequestId,
    IReadOnlyList<AnnualBackupPlan> AnnualArchives);

public sealed record BackupReportCommand(
    bool Success,
    string? FileName,
    string? ManifestSha256,
    int? FormatVersion,
    string? FailureCode,
    Guid? ManualRequestId,
    Guid? AnnualRequestId);

public sealed record BackupPayloadResult(
    int FormatVersion,
    string ManifestSha256,
    DateTimeOffset CreatedAtUtc);

public sealed record BackupManifestEntry(string Path, long Length, string Sha256);

public sealed record BackupManifest(
    int FormatVersion,
    DateTimeOffset CreatedAtUtc,
    string ApplicationVersion,
    IReadOnlyList<BackupManifestEntry> Entries,
    IReadOnlyDictionary<string, long> TableRecordCounts);

public interface IBackupService
{
    Task<BackupOverview> GetOverviewAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IssuedBackupToken> IssueTokenAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<Guid> RequestNowAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<BackupAuthorization?> AuthorizeAsync(
        string rawToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<BackupPlan> GetPlanAsync(
        BackupAuthorization authorization,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ReportAsync(
        BackupAuthorization authorization,
        BackupReportCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public interface IBackupPayloadWriter
{
    Task<BackupPayloadResult> WriteAsync(
        string ownerUserId,
        Stream destination,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
