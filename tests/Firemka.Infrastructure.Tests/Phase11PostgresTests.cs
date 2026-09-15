using System.Diagnostics;
using Firemka.Application.Backups;
using Firemka.BackupClient;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase11PostgresTests
{
    private const string PreviousMigration = "20260914162642_Phase10PaymentsNotifications";
    private const string Password = "synthetic-phase11-recovery-password!";

    [PostgresFact]
    public async Task Migration_full_encrypted_backup_and_clean_restore_preserve_all_counts_and_files()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var sourceName = $"firemka_phase11_source_{Guid.NewGuid():N}";
        var targetName = $"firemka_phase11_target_{Guid.NewGuid():N}";
        var root = Path.Combine(Path.GetTempPath(), $"firemka-phase11-pg-{Guid.NewGuid():N}");
        var privateFiles = Path.Combine(root, "private");
        var keys = Path.Combine(root, "keys");
        var backups = Path.Combine(root, "backups");
        var extracted = Path.Combine(root, "extracted");
        Directory.CreateDirectory(privateFiles);
        Directory.CreateDirectory(keys);
        Directory.CreateDirectory(backups);
        await File.WriteAllTextAsync(Path.Combine(privateFiles, "synthetic.xml"), "<Invoice>synthetic</Invoice>");
        await File.WriteAllTextAsync(Path.Combine(keys, "key.xml"), "<key>synthetic</key>");
        await CreateDatabaseAsync(administrativeConnection, sourceName);
        await CreateDatabaseAsync(administrativeConnection, targetName);
        var sourceConnection = WithDatabase(administrativeConnection, sourceName);
        var targetConnection = WithDatabase(administrativeConnection, targetName);

        try
        {
            await using (var migration = CreateDb(sourceConnection))
            {
                await migration.Database.MigrateAsync();
                Assert.True(await TableExistsAsync(sourceConnection, "BackupStates"));
                var migrator = migration.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration);
                Assert.False(await TableExistsAsync(sourceConnection, "BackupStates"));
                await migrator.MigrateAsync();
            }
            await using (var seed = CreateDb(sourceConnection))
            {
                _ = await new BackupService(seed).IssueTokenAsync(
                    "owner-phase11", DateTimeOffset.Parse("2026-09-14T12:00:00Z"));
            }

            byte[] payload;
            await using (var db = CreateDb(sourceConnection))
            await using (var output = new MemoryStream())
            {
                var options = new BackupPayloadOptions
                {
                    DataProtectionKeyRingPath = keys,
                    PgDumpPath = FindExecutable("pg_dump"),
                };
                var writer = new BackupPayloadWriter(
                    new PostgresBackupDatabaseSnapshotSource(db, options),
                    new PrivateFileStoreOptions { RootPath = privateFiles }, options);
                await writer.WriteAsync("owner-phase11", output,
                    DateTimeOffset.Parse("2026-09-14T12:01:00Z"));
                payload = output.ToArray();
            }

            var saved = await BackupFileManager.SaveVerifiedAsync(
                new MemoryStream(payload), backups, Password,
                DateTimeOffset.Parse("2026-09-14T12:02:00Z"));
            var verified = await BackupRestoreExtractor.ExtractVerifiedAsync(
                saved.FullPath, extracted, Password);
            await RestoreDumpAsync(targetConnection, Path.Combine(extracted, "database", "firemka.dump"));

            await using (var target = CreateDb(targetConnection))
            {
                foreach (var expected in verified.Manifest.TableRecordCounts)
                {
                    var actual = await CountTableAsync(targetConnection, expected.Key);
                    Assert.Equal(expected.Value, actual);
                }
                Assert.Single(await target.BackupAccessTokens.AsNoTracking().ToListAsync());
                Assert.Single(await target.BackupStates.AsNoTracking().ToListAsync());
            }
            Assert.Equal("<Invoice>synthetic</Invoice>",
                await File.ReadAllTextAsync(Path.Combine(extracted, "private-files", "synthetic.xml")));
            Assert.Equal("<key>synthetic</key>",
                await File.ReadAllTextAsync(Path.Combine(extracted, "data-protection-keys", "key.xml")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            await DropDatabaseAsync(administrativeConnection, sourceName);
            await DropDatabaseAsync(administrativeConnection, targetName);
        }
    }

    [PostgresFact]
    public async Task Concurrent_token_and_manual_requests_leave_one_active_record_without_server_errors()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase11_race_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = WithDatabase(administrativeConnection, databaseName);
        const string owner = "owner-phase11-race";
        var now = DateTimeOffset.Parse("2026-09-14T12:00:00Z");
        try
        {
            await using (var migration = CreateDb(connection))
                await migration.Database.MigrateAsync();

            Guid[] requestIds;
            await using (var first = CreateDb(connection))
            await using (var second = CreateDb(connection))
            {
                requestIds = await Task.WhenAll(
                    new BackupService(first).RequestNowAsync(owner, now),
                    new BackupService(second).RequestNowAsync(owner, now));
            }
            Assert.Equal(requestIds[0], requestIds[1]);

            IssuedBackupToken[] issued;
            await using (var first = CreateDb(connection))
            await using (var second = CreateDb(connection))
            {
                issued = await Task.WhenAll(
                    new BackupService(first).IssueTokenAsync(owner, now.AddMinutes(1)),
                    new BackupService(second).IssueTokenAsync(owner, now.AddMinutes(1)));
            }

            await using var verification = CreateDb(connection);
            Assert.Single(await verification.BackupRequests.Where(item => item.CompletedAtUtc == null).ToListAsync());
            Assert.Single(await verification.BackupAccessTokens.Where(item => item.RevokedAtUtc == null).ToListAsync());
            var valid = 0;
            foreach (var token in issued)
            {
                await using var authorizationDb = CreateDb(connection);
                if (await new BackupService(authorizationDb).AuthorizeAsync(token.RawToken, now.AddMinutes(2)) is not null)
                    valid++;
            }
            Assert.Equal(1, valid);
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static AppDbContext CreateDb(string connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static string WithDatabase(string connection, string database)
        => new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString;

    private static async Task RestoreDumpAsync(string connectionString, string dumpPath)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FindExecutable("pg_restore"),
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--exit-on-error");
        process.StartInfo.ArgumentList.Add("--no-owner");
        process.StartInfo.ArgumentList.Add("--no-privileges");
        process.StartInfo.ArgumentList.Add("--dbname");
        process.StartInfo.ArgumentList.Add(builder.Database!);
        process.StartInfo.ArgumentList.Add(dumpPath);
        process.StartInfo.Environment["PGHOST"] = builder.Host;
        process.StartInfo.Environment["PGPORT"] = builder.Port.ToString();
        process.StartInfo.Environment["PGDATABASE"] = builder.Database;
        process.StartInfo.Environment["PGUSER"] = builder.Username;
        process.StartInfo.Environment["PGPASSWORD"] = builder.Password;
        Assert.True(process.Start());
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await errorTask);
    }

    private static string FindExecutable(string name)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(folder, name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException($"Nie znaleziono {name} w PATH.");
    }

    private static async Task<long> CountTableAsync(string connectionString, string table)
    {
        Assert.Matches("^[A-Za-z0-9_]+$", table);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select to_regclass('public.\"' || @name || '\"') is not null", connection);
        command.Parameters.AddWithValue("name", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
