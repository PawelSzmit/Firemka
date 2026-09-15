using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Filings.Zus;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase8PostgresTests
{
    private const string PreviousMigration = "20260913104501_Phase7SourceConflictHistory";
    private const string Owner = "owner-phase8-postgres";
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T08:00:00Z");

    [PostgresFact]
    public async Task Migration_and_forced_concurrency_preserve_artifact_profile_and_receipt_integrity()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase8_{Guid.NewGuid():N}";
        var fileRoot = Path.Combine(Path.GetTempPath(), $"firemka-phase8-pg-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (var migration = CreateDb(connection))
            {
                await migration.Database.MigrateAsync();
                Assert.True(await TableExistsAsync(connection, "FilingArtifacts"));
                Assert.True(await TableExistsAsync(connection, "FilingProfileVersions"));
                Assert.True(await ColumnExistsAsync(connection, "FilingArtifacts", "FilingProfileVersionId"));
                Assert.True(await ColumnExistsAsync(connection, "SourceDocuments", "SellerAddress"));
                var migrator = migration.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration);
                Assert.False(await TableExistsAsync(connection, "FilingArtifacts"));
                Assert.False(await TableExistsAsync(connection, "FilingProfileVersions"));
                Assert.False(await ColumnExistsAsync(connection, "SourceDocuments", "SellerAddress"));
                await migrator.MigrateAsync();
            }

            var companyId = await SeedClosedMonthAndProfileAsync(connection, fileRoot);
            await InstallArtifactRaceDelayAsync(connection);
            IReadOnlyList<FilingArtifactSnapshot>[] results;
            try
            {
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    results = await Task.WhenAll(
                        CreateFilingService(first, fileRoot).GenerateAsync(
                            Owner, companyId, September, Now.AddMinutes(1)),
                        CreateFilingService(second, fileRoot).GenerateAsync(
                            Owner, companyId, September, Now.AddMinutes(1)));
                }

                Assert.Equal(3, results[0].Count);
                Assert.Equal(
                    results[0].Select(item => item.Id).OrderBy(item => item),
                    results[1].Select(item => item.Id).OrderBy(item => item));
            }
            finally
            {
                await RemoveArtifactRaceDelayAsync(connection);
            }

            await using (var verification = CreateDb(connection))
            {
                Assert.Equal(3, await verification.FilingArtifacts.CountAsync());
                Assert.Equal(3, await verification.StoredFiles.CountAsync(item =>
                    item.RecordType == StoredFileRecordType.FilingArtifactVersion));
                Assert.Equal(3, await verification.AuditEvents.CountAsync(item =>
                    item.Action == "filing-artifact-prepared"));
            }

            var vatId = results[0].Single(item => item.Kind == FilingArtifactKind.JpkV7M3).Id;
            var zusId = results[0].Single(item => item.Kind == FilingArtifactKind.ZusDraKedu227).Id;
            await using (var actions = CreateDb(connection))
            {
                var service = CreateFilingService(actions, fileRoot);
                await service.ApproveAsync(Owner, vatId, "synthetic:vat-reviewed", Now.AddMinutes(2));
                await service.MarkSentAsync(Owner, vatId, "synthetic:vat-manual-send", Now.AddMinutes(3));
                await service.ApproveAsync(Owner, zusId, "synthetic:zus-reviewed", Now.AddMinutes(2));
                await service.MarkSentAsync(Owner, zusId, "synthetic:zus-manual-send", Now.AddMinutes(3));
            }

            await InstallOutcomeRaceDelayAsync(connection);
            try
            {
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    var sameReceipt = "<UPO>synthetic-same</UPO>"u8.ToArray();
                    var same = await Task.WhenAll(
                        RecordOutcomeAsync(first, fileRoot, vatId, FilingSubmissionOutcome.Accepted,
                            sameReceipt, "UPO-SAME"),
                        RecordOutcomeAsync(second, fileRoot, vatId, FilingSubmissionOutcome.Accepted,
                            sameReceipt, "UPO-SAME"));
                    Assert.Equal(same[0].ReceiptFileId, same[1].ReceiptFileId);
                }

                Exception? firstError;
                Exception? secondError;
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    var errors = await Task.WhenAll(
                        CaptureAsync(() => RecordOutcomeAsync(first, fileRoot, zusId,
                            FilingSubmissionOutcome.Accepted, "<UPO>A</UPO>"u8.ToArray(), "UPO-A")),
                        CaptureAsync(() => RecordOutcomeAsync(second, fileRoot, zusId,
                            FilingSubmissionOutcome.Rejected, "<UPO>B</UPO>"u8.ToArray(), "UPO-B")));
                    firstError = errors[0];
                    secondError = errors[1];
                }
                Assert.Equal(1, new[] { firstError, secondError }.Count(item => item is null));
                Assert.Single(new[] { firstError, secondError }, item => item is InvalidOperationException);
            }
            finally
            {
                await RemoveOutcomeRaceDelayAsync(connection);
            }

            Guid correctedProfileId;
            await using (var profileDb = CreateDb(connection))
            {
                var service = CreateFilingService(profileDb, fileRoot);
                correctedProfileId = (await service.SaveProfileAsync(
                    Owner,
                    companyId,
                    ProfileCommand() with { TaxOfficeCode = "0202" },
                    Now.AddMinutes(4))).Id;
            }
            IReadOnlyList<FilingArtifactSnapshot> corrected;
            await using (var regenerateDb = CreateDb(connection))
            {
                corrected = await CreateFilingService(regenerateDb, fileRoot).GenerateAsync(
                    Owner, companyId, September, Now.AddMinutes(5));
            }
            Assert.All(corrected, item =>
            {
                Assert.Equal(2, item.VersionNumber);
                Assert.Equal(correctedProfileId, item.FilingProfileVersionId);
                Assert.NotNull(item.PreviousArtifactId);
            });

            await using (var finalVerification = CreateDb(connection))
            {
                Assert.Equal(6, await finalVerification.FilingArtifacts.CountAsync());
                Assert.Equal(2, await finalVerification.StoredFiles.CountAsync(item =>
                    item.RecordType == StoredFileRecordType.SubmissionReceipt));
            }
            Assert.Equal(8, Directory.GetFiles(fileRoot, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            if (Directory.Exists(fileRoot)) Directory.Delete(fileRoot, recursive: true);
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static async Task<Guid> SeedClosedMonthAndProfileAsync(string connection, string fileRoot)
    {
        Guid companyId;
        await using (var db = CreateDb(connection))
        {
            var company = Company.Register(
                Owner,
                "Syntetyczna Firma Fazy 8",
                "1010000000",
                "Testowy adres firmy",
                September,
                "Syntetyczny Klient",
                "1234567890",
                "Adres testowego klienta",
                "Usługa testowa",
                1_000m,
                23m,
                1m,
                VehicleArrangement.None,
                Now);
            var invoice = SalesInvoice.CreateDraft(
                company.Id,
                Owner,
                September,
                company.GetSubscriptionRate(September).Id,
                company.ServiceDescription,
                1_000m,
                23m,
                Now);
            invoice.PrepareForIssue(September, "FV/09/2026", false, "<Invoice />", Now);
            invoice.RegisterSubmission("session-test", "submission-test", Now);
            invoice.MarkIssued(
                "1010000000-20260101-000000000000-00",
                "<UPO />",
                "<Invoice />",
                Now);
            db.Companies.Add(company);
            db.SalesInvoices.Add(invoice);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        await using (var db = CreateDb(connection))
        {
            var closing = new MonthClosingService(db, new AuditTrail(db));
            var view = await closing.GetAsync(Owner, companyId, September, Now);
            await closing.ConfirmRuleSetAsync(
                Owner,
                view!.RuleSet!.Id,
                new ConfirmCalculationRuleSetCommand(
                    true,
                    "synthetic:independent-postgres-review",
                    new DateOnly(2026, 9, 12)),
                Now);
            await closing.SaveDeclarationAsync(
                Owner,
                companyId,
                September,
                new SaveMonthDeclarationCommand(
                    0m, 0m, 0m, 0m, 0m,
                    true,
                    true,
                    "synthetic:month-ready"),
                Now);
            await closing.CloseAsync(Owner, companyId, September, Now);
        }

        await using (var db = CreateDb(connection))
        {
            await CreateFilingService(db, fileRoot).SaveProfileAsync(
                Owner,
                companyId,
                new SaveFilingProfileCommand(
                    "Jan", "Testowy", new DateOnly(1990, 1, 1), "90010112345",
                    "1215", "0510", "jan@example.test",
                    "synthetic:profile-reviewed", new DateOnly(2026, 9, 12)),
                Now);
        }

        return companyId;
    }

    private static FilingService CreateFilingService(AppDbContext db, string fileRoot)
        => new(
            db,
            new PrivateFileStore(new PrivateFileStoreOptions
            {
                RootPath = fileRoot,
                MaximumFileSizeBytes = 5_000_000,
            }),
            new JpkV7M3Generator(),
            new JpkPkpir3Generator(),
            new ZusDraKedu227Generator(),
            new AuditTrail(db));

    private static Task<FilingArtifactSnapshot> RecordOutcomeAsync(
        AppDbContext db,
        string fileRoot,
        Guid artifactId,
        FilingSubmissionOutcome outcome,
        byte[] content,
        string reference)
        => CreateFilingService(db, fileRoot).RecordOutcomeAsync(
            Owner,
            artifactId,
            outcome,
            new FilingReceiptUpload("upo.xml", "application/xml", new MemoryStream(content)),
            reference,
            Now.AddMinutes(4));

    private static async Task<Exception?> CaptureAsync(Func<Task<FilingArtifactSnapshot>> action)
    {
        try
        {
            _ = await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static SaveFilingProfileCommand ProfileCommand() => new(
        "Jan", "Testowy", new DateOnly(1990, 1, 1), "90010112345",
        "1215", "0510", "jan@example.test",
        "synthetic:profile-reviewed", new DateOnly(2026, 9, 12));

    private static AppDbContext CreateDb(string connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static Task InstallArtifactRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            CREATE OR REPLACE FUNCTION phase8_delay_artifact() RETURNS trigger AS $$
            BEGIN
                PERFORM pg_sleep(0.4);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase8_delay_artifact_trigger
                BEFORE INSERT ON "FilingArtifacts"
                FOR EACH ROW EXECUTE FUNCTION phase8_delay_artifact();
            """);

    private static Task RemoveArtifactRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            DROP TRIGGER IF EXISTS phase8_delay_artifact_trigger ON "FilingArtifacts";
            DROP FUNCTION IF EXISTS phase8_delay_artifact();
            """);

    private static Task InstallOutcomeRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            CREATE OR REPLACE FUNCTION phase8_delay_outcome() RETURNS trigger AS $$
            BEGIN
                IF OLD."Status" = 'Sent' THEN
                    PERFORM pg_sleep(0.4);
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase8_delay_outcome_trigger
                BEFORE UPDATE ON "FilingArtifacts"
                FOR EACH ROW EXECUTE FUNCTION phase8_delay_outcome();
            """);

    private static Task RemoveOutcomeRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            DROP TRIGGER IF EXISTS phase8_delay_outcome_trigger ON "FilingArtifacts";
            DROP FUNCTION IF EXISTS phase8_delay_outcome();
            """);

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select to_regclass('public.\"' || @name || '\"') is not null",
            connection);
        command.Parameters.AddWithValue("name", tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ColumnExistsAsync(
        string connectionString,
        string tableName,
        string columnName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
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
