namespace Firemka.Infrastructure.Backups;

public sealed class BackupPayloadOptions
{
    public const string SectionName = "BackupPayload";

    public string DataProtectionKeyRingPath { get; set; } = string.Empty;
    public string PgDumpPath { get; set; } = "pg_dump";
}
