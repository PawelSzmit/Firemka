namespace Firemka.Infrastructure.Backups;

public interface IBackupDatabaseSnapshotSource
{
    Task<IReadOnlyDictionary<string, long>> WriteConsistentDumpAsync(
        Stream destination,
        CancellationToken cancellationToken = default);
}
