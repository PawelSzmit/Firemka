using Firemka.Application.AnnualClosing;
using Firemka.Application.Filings;
using Firemka.Application.MonthClosing;
using Firemka.Domain.Companies;
using Firemka.Domain.Filings;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.AnnualClosing;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Filings.Jpk;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase9PostgresTests
{
    private const string PreviousMigration = "20260913160117_Phase8ManualFilings";
    private const string Owner = "owner-phase9-postgres";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2027-01-10T10:00:00Z");

    [PostgresFact]
    public async Task Migration_concurrent_close_and_jpk_history_are_durable()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase9_{Guid.NewGuid():N}";
        var fileRoot = Path.Combine(Path.GetTempPath(), $"firemka-phase9-pg-{Guid.NewGuid():N}");
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
                Assert.True(await TableExistsAsync(connection, "AnnualClosings"));
                Assert.True(await ColumnExistsAsync(connection, "AnnualClosings", "JpkStatus"));
                Assert.True(await ColumnExistsAsync(connection, "AnnualClosings", "HealthPaymentDifference"));
                var migrator = migration.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration);
                Assert.False(await TableExistsAsync(connection, "AnnualClosings"));
                await migrator.MigrateAsync();
            }

            var companyId = await SeedReadyYearAsync(connection);
            AnnualClosingSnapshot[] closings;
            await using (var first = CreateDb(connection))
            await using (var second = CreateDb(connection))
            {
                closings = await Task.WhenAll(
                    CreateService(first, fileRoot).CloseAsync(
                        Owner, companyId, 2026, new ConfirmAnnualClosingCommand(true), Now),
                    CreateService(second, fileRoot).CloseAsync(
                        Owner, companyId, 2026, new ConfirmAnnualClosingCommand(true), Now));
            }

            Assert.Equal(closings[0].Id, closings[1].Id);
            await using (var verification = CreateDb(connection))
            {
                Assert.Single(await verification.AnnualClosings.ToListAsync());
                Assert.Equal(2, await verification.StoredFiles.CountAsync());
                Assert.Single(await verification.AnnualArchiveRequests.ToListAsync());
            }

            await using (var actions = CreateDb(connection))
            {
                var service = CreateService(actions, fileRoot);
                await service.ApproveJpkAsync(Owner, closings[0].Id, "synthetic:reviewed", Now.AddMinutes(1));
                await service.MarkJpkSentAsync(Owner, closings[0].Id, "synthetic:manual-send", Now.AddMinutes(2));
                await using var receipt = new MemoryStream("<UPO>synthetic</UPO>"u8.ToArray());
                await service.RecordJpkOutcomeAsync(
                    Owner, closings[0].Id, FilingSubmissionOutcome.Accepted,
                    new FilingReceiptUpload("upo.xml", "application/xml", receipt),
                    "UPO-PHASE9", Now.AddMinutes(3));
            }

            await using (var final = CreateDb(connection))
            {
                var closing = await final.AnnualClosings.AsNoTracking().SingleAsync();
                Assert.Equal(FilingArtifactStatus.Accepted, closing.JpkStatus);
                Assert.NotNull(closing.JpkReceiptStoredFileId);
                Assert.Equal(3, await final.StoredFiles.CountAsync());
            }
            Assert.Equal(3, Directory.GetFiles(fileRoot, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            if (Directory.Exists(fileRoot)) Directory.Delete(fileRoot, recursive: true);
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    [PostgresFact]
    public async Task Forced_annual_races_are_idempotent_and_never_leave_a_closed_year_with_an_open_month()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase9_race_{Guid.NewGuid():N}";
        var fileRoot = Path.Combine(Path.GetTempPath(), $"firemka-phase9-race-{Guid.NewGuid():N}");
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (var migration = CreateDb(connection)) await migration.Database.MigrateAsync();
            var companyId = await SeedReadyYearAsync(connection);
            AnnualClosingSnapshot firstClosing;
            await using (var db = CreateDb(connection))
            {
                var service = CreateService(db, fileRoot);
                firstClosing = await service.CloseAsync(
                    Owner, companyId, 2026, new ConfirmAnnualClosingCommand(true), Now);
                await service.ApproveJpkAsync(
                    Owner, firstClosing.Id, "synthetic:reviewed-v1", Now.AddMinutes(1));
                await service.MarkJpkSentAsync(
                    Owner, firstClosing.Id, "synthetic:sent-v1", Now.AddMinutes(2));
            }

            await InstallOutcomeRaceDelayAsync(connection);
            try
            {
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    var receipt = "<UPO>same-v1</UPO>"u8.ToArray();
                    var same = await Task.WhenAll(
                        RecordOutcomeAsync(first, fileRoot, firstClosing.Id,
                            FilingSubmissionOutcome.Accepted, receipt, "UPO-SAME-V1"),
                        RecordOutcomeAsync(second, fileRoot, firstClosing.Id,
                            FilingSubmissionOutcome.Accepted, receipt, "UPO-SAME-V1"));
                    Assert.Equal(same[0].JpkReceiptStoredFileId, same[1].JpkReceiptStoredFileId);
                }
                await RemoveOutcomeRaceDelayAsync(connection);

                AnnualClosingSnapshot[] corrections;
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    corrections = await Task.WhenAll(
                        CreateService(first, fileRoot).StartCorrectionAsync(
                            Owner, companyId, 2026, "synthetic:same-correction", Now.AddMinutes(4)),
                        CreateService(second, fileRoot).StartCorrectionAsync(
                            Owner, companyId, 2026, "synthetic:same-correction", Now.AddMinutes(4)));
                }
                Assert.Equal(corrections[0].Id, corrections[1].Id);

                AnnualClosingSnapshot secondClosing;
                await using (var closeDb = CreateDb(connection))
                {
                    secondClosing = await CreateService(closeDb, fileRoot).CloseAsync(
                        Owner, companyId, 2026, new ConfirmAnnualClosingCommand(true), Now.AddMinutes(5));
                }
                await using (var actions = CreateDb(connection))
                {
                    var service = CreateService(actions, fileRoot);
                    var obsolete = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        service.ApproveJpkAsync(
                            Owner, firstClosing.Id, "synthetic:obsolete", Now.AddMinutes(6)));
                    Assert.Contains("najnowszej wersji", obsolete.Message, StringComparison.OrdinalIgnoreCase);
                    await service.ApproveJpkAsync(
                        Owner, secondClosing.Id, "synthetic:reviewed-v2", Now.AddMinutes(6));
                    await service.MarkJpkSentAsync(
                        Owner, secondClosing.Id, "synthetic:sent-v2", Now.AddMinutes(7));
                }

                Exception?[] outcomeErrors;
                await InstallOutcomeRaceDelayAsync(connection);
                await using (var first = CreateDb(connection))
                await using (var second = CreateDb(connection))
                {
                    outcomeErrors = await Task.WhenAll(
                        CaptureAsync(() => RecordOutcomeAsync(
                            first, fileRoot, secondClosing.Id, FilingSubmissionOutcome.Accepted,
                            "<UPO>A</UPO>"u8.ToArray(), "UPO-A")),
                        CaptureAsync(() => RecordOutcomeAsync(
                            second, fileRoot, secondClosing.Id, FilingSubmissionOutcome.Rejected,
                            "<UPO>B</UPO>"u8.ToArray(), "UPO-B")));
                }
                Assert.Equal(1, outcomeErrors.Count(item => item is null));
                Assert.Single(outcomeErrors, item => item is InvalidOperationException);
                await RemoveOutcomeRaceDelayAsync(connection);

                await using (var correctionDb = CreateDb(connection))
                {
                    await CreateService(correctionDb, fileRoot).StartCorrectionAsync(
                        Owner, companyId, 2026, "synthetic:race-with-month", Now.AddMinutes(8));
                }

                bool[] raceResults;
                await using (var annualDb = CreateDb(connection))
                await using (var monthDb = CreateDb(connection))
                {
                    raceResults = await Task.WhenAll(
                        CaptureSuccessAsync(() => CreateService(annualDb, fileRoot).CloseAsync(
                            Owner, companyId, 2026, new ConfirmAnnualClosingCommand(true), Now.AddMinutes(9))),
                        CaptureSuccessAsync(() => new MonthClosingService(monthDb, new AuditTrail(monthDb))
                            .StartCorrectionAsync(Owner, companyId, new DateOnly(2026, 12, 1),
                                "synthetic:december-correction", Now.AddMinutes(9))));
                }
                Assert.Equal(1, raceResults.Count(result => result));
            }
            finally
            {
                await RemoveOutcomeRaceDelayAsync(connection);
            }

            await using (var final = CreateDb(connection))
            {
                var annualVersions = await final.AnnualClosings.AsNoTracking()
                    .Include(item => item.Months)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync();
                Assert.Equal(3, annualVersions[0].Months.Count);
                Assert.Equal(3, annualVersions[1].Months.Count);
                Assert.Empty(annualVersions[0].Months.Select(item => item.Id)
                    .Intersect(annualVersions[1].Months.Select(item => item.Id)));
                var annual = annualVersions
                    .OrderByDescending(item => item.VersionNumber).First();
                var december = await final.MonthSettlements.AsNoTracking()
                    .Where(item => item.Month == new DateOnly(2026, 12, 1))
                    .OrderByDescending(item => item.VersionNumber).FirstAsync();
                Assert.False(
                    annual.Status == Firemka.Domain.AnnualClosing.AnnualClosingStatus.Closed
                    && december.Status == Firemka.Domain.Calculations.MonthSettlementStatus.OpenCorrection);
                Assert.Equal(2, await final.StoredFiles.CountAsync(item =>
                    item.RecordType == StoredFileRecordType.SubmissionReceipt));
                Assert.Equal(
                    await final.StoredFiles.CountAsync(),
                    Directory.GetFiles(fileRoot, "*", SearchOption.AllDirectories).Length);
            }
        }
        finally
        {
            if (Directory.Exists(fileRoot)) Directory.Delete(fileRoot, recursive: true);
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static async Task<AnnualClosingSnapshot> RecordOutcomeAsync(
        AppDbContext db, string fileRoot, Guid closingId, FilingSubmissionOutcome outcome,
        byte[] content, string reference)
    {
        await using var receipt = new MemoryStream(content);
        return await CreateService(db, fileRoot).RecordJpkOutcomeAsync(
            Owner, closingId, outcome,
            new FilingReceiptUpload("upo.xml", "application/xml", receipt),
            reference, Now.AddMinutes(10));
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<AnnualClosingSnapshot>> action)
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

    private static async Task<bool> CaptureSuccessAsync<T>(Func<Task<T>> action)
    {
        try
        {
            _ = await action();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or AnnualClosingBlockedException)
        {
            return false;
        }
    }

    private static async Task<Guid> SeedReadyYearAsync(string connection)
    {
        Guid companyId;
        await using (var db = CreateDb(connection))
        {
            var october = new DateOnly(2026, 10, 1);
            var company = Company.Register(
                Owner, "Syntetyczna Firma Fazy 9", "1010000000", "Adres firmy", october,
                "Syntetyczny Klient", "1234567890", "Adres klienta", "Usługa testowa",
                1_000m, 23m, 1m, VehicleArrangement.None, Now);
            db.Companies.Add(company);
            foreach (var month in Enumerable.Range(10, 3).Select(number => new DateOnly(2026, number, 1)))
            {
                var invoice = SalesInvoice.CreateDraft(company.Id, Owner, month,
                    company.GetSubscriptionRate(month).Id, company.ServiceDescription, 1_000m, 23m, Now);
                invoice.PrepareForIssue(month, $"FV/{month:yyyyMM}", false, "<Invoice />", Now);
                invoice.RegisterSubmission($"session-{month:MM}", $"submission-{month:MM}", Now);
                invoice.MarkIssued($"1010000000-2026{month:MM}01-000000000000-00", "<UPO />", "<Invoice />", Now);
                db.SalesInvoices.Add(invoice);
            }
            db.FilingProfileVersions.Add(FilingProfileVersion.Create(
                company.Id, Owner, 1, null, "Jan", "Testowy", new DateOnly(1990, 1, 1),
                "90010112345", "1215", "0510", "jan@example.test", "synthetic:profile",
                new DateOnly(2027, 1, 10), Now, new DateOnly(2027, 1, 10)));
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        await using (var db = CreateDb(connection))
        {
            var closing = new MonthClosingService(db, new AuditTrail(db));
            var first = await closing.GetAsync(Owner, companyId, new DateOnly(2026, 10, 1), Now);
            await closing.ConfirmRuleSetAsync(Owner, first!.RuleSet!.Id,
                new ConfirmCalculationRuleSetCommand(true, "synthetic:rules", new DateOnly(2026, 12, 31)), Now);
            foreach (var month in Enumerable.Range(10, 3).Select(number => new DateOnly(2026, number, 1)))
            {
                await closing.SaveDeclarationAsync(Owner, companyId, month,
                    new SaveMonthDeclarationCommand(0m, 0m, 0m,
                        month.Month == 10 ? 0m : null, month.Month == 10 ? 0m : null,
                        month.Month == 10, true, "synthetic:month"), Now);
                await closing.CloseAsync(Owner, companyId, month, Now);
            }
        }

        await using (var db = CreateDb(connection))
        {
            await CreateService(db, Path.Combine(Path.GetTempPath(), "unused-phase9-files"))
                .SaveDeclarationAsync(Owner, companyId, 2026,
                    new SaveAnnualDeclarationCommand(
                        0m, 0m, 0m, 0m, true, "synthetic:annual",
                        new DateOnly(2027, 1, 10)), Now);
        }
        return companyId;
    }

    private static AnnualClosingService CreateService(AppDbContext db, string fileRoot)
        => new(db, new PrivateFileStore(new PrivateFileStoreOptions
        {
            RootPath = fileRoot,
            MaximumFileSizeBytes = 5_000_000,
        }),
            new JpkPkpir3Generator(), new AnnualReportPdfGenerator(),
            new MonthClosingService(db, new AuditTrail(db)),
            new AnnualArchiveRequestQueue(db), new AuditTrail(db));

    private static AppDbContext CreateDb(string connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static Task InstallOutcomeRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            CREATE OR REPLACE FUNCTION phase9_delay_outcome() RETURNS trigger AS $$
            BEGIN
                IF OLD."JpkStatus" = 'Sent' THEN
                    PERFORM pg_sleep(0.4);
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase9_delay_outcome_trigger
                BEFORE UPDATE ON "AnnualClosings"
                FOR EACH ROW EXECUTE FUNCTION phase9_delay_outcome();
            """);

    private static Task RemoveOutcomeRaceDelayAsync(string connection)
        => ExecuteSqlAsync(connection,
            """
            DROP TRIGGER IF EXISTS phase9_delay_outcome_trigger ON "AnnualClosings";
            DROP FUNCTION IF EXISTS phase9_delay_outcome();
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
            "select to_regclass('public.\"' || @name || '\"') is not null", connection);
        command.Parameters.AddWithValue("name", tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ColumnExistsAsync(
        string connectionString, string tableName, string columnName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)
            """, connection);
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
