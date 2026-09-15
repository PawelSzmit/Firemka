using System.Data;
using System.Diagnostics;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Firemka.Infrastructure.Backups;

public sealed class PostgresBackupDatabaseSnapshotSource(
    AppDbContext dbContext,
    BackupPayloadOptions options) : IBackupDatabaseSnapshotSource
{
    public async Task<IReadOnlyDictionary<string, long>> WriteConsistentDumpAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("Strumień kopii nie jest zapisywalny.", nameof(destination));
        if (!dbContext.Database.IsNpgsql())
            throw new InvalidOperationException("Pełna kopia bazy wymaga PostgreSQL.");

        var connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Brakuje połączenia PostgreSQL.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        string snapshot;
        await using (var export = new NpgsqlCommand("SELECT pg_export_snapshot()", connection, transaction))
            snapshot = (string)(await export.ExecuteScalarAsync(cancellationToken))!;
        var counts = await ReadCountsAsync(connection, transaction, cancellationToken);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(builder, snapshot),
            EnableRaisingEvents = true,
        };
        if (!process.Start()) throw new InvalidOperationException("Nie udało się uruchomić pg_dump.");
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(destination, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }

        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pg_dump nie utworzył kopii: {SafeError(stderr)}");
        await transaction.CommitAsync(cancellationToken);
        return counts;
    }

    private ProcessStartInfo CreateStartInfo(NpgsqlConnectionStringBuilder builder, string snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PgDumpPath);
        var start = new ProcessStartInfo
        {
            FileName = options.PgDumpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--format=custom");
        start.ArgumentList.Add("--no-owner");
        start.ArgumentList.Add("--no-privileges");
        start.ArgumentList.Add($"--snapshot={snapshot}");
        start.Environment["PGHOST"] = builder.Host;
        start.Environment["PGPORT"] = builder.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["PGDATABASE"] = builder.Database;
        start.Environment["PGUSER"] = builder.Username;
        start.Environment["PGPASSWORD"] = builder.Password;
        start.Environment["PGCONNECT_TIMEOUT"] = "15";
        if (builder.SslMode != SslMode.Disable)
            start.Environment["PGSSLMODE"] = builder.SslMode switch
            {
                SslMode.Allow => "allow",
                SslMode.Prefer => "prefer",
                SslMode.Require => "require",
                SslMode.VerifyCA => "verify-ca",
                SslMode.VerifyFull => "verify-full",
                _ => throw new InvalidOperationException("Nieobsługiwany tryb TLS połączenia PostgreSQL."),
            };
        return start;
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadCountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        await using (var tableCommand = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename",
            connection, transaction))
        await using (var reader = await tableCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
        }

        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var quoted = '"' + table.Replace("\"", "\"\"") + '"';
            await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM {quoted}", connection, transaction);
            result[table] = (long)(await count.ExecuteScalarAsync(cancellationToken))!;
        }
        return result;
    }

    private static string SafeError(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
