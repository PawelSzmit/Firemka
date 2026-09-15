using Firemka.Application.MonthClosing;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase7PostgresTests
{
    private const string Phase6Migration = "20260912095401_Phase6CostAccountingVehiclesCharging";
    private const string ConflictResolutionMigration = "20260913102725_Phase7SourceConflictResolution";
    private const string Owner = "owner-phase7-postgres";
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset OctoberNow = DateTimeOffset.Parse("2026-10-02T08:00:00Z");

    [PostgresFact]
    public async Task Migration_round_trip_and_concurrent_commands_keep_one_immutable_chain()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase7_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (var migrationContext = CreateDbContext(connection))
            {
                await migrationContext.Database.MigrateAsync();
            }

            foreach (var table in Phase7Tables)
            {
                Assert.True(await TableExistsAsync(connection, table), $"Missing table {table}.");
            }
            Assert.True(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictResolution"));
            Assert.True(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictResolvedAtUtc"));
            Assert.True(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictSha256"));

            await using (var migrationContext = CreateDbContext(connection))
            {
                var migrator = migrationContext.GetService<IMigrator>();
                await migrator.MigrateAsync(Phase6Migration);
                foreach (var table in Phase7Tables)
                {
                    Assert.False(await TableExistsAsync(connection, table), $"Table {table} survived rollback.");
                }
                Assert.False(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictResolution"));
                Assert.False(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictResolvedAtUtc"));
                Assert.False(await ColumnExistsAsync(connection, "SourceDocuments", "SourceConflictSha256"));

                Assert.True(await TableExistsAsync(connection, "ChargingSessions"));
                await migrator.MigrateAsync(ConflictResolutionMigration);
                var legacyConflictId = Guid.NewGuid();
                await migrationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "SourceDocuments"
                        ("Id", "OwnerUserId", "Origin", "Status", "StateVersion",
                         "DataRevisionNumber", "HasSourceConflict", "CreatedAtUtc", "UpdatedAtUtc",
                         "SourceConflictDetectedAtUtc", "SourceConflictResolvedAtUtc", "SourceConflictResolution")
                    VALUES
                        ({legacyConflictId}, {Owner}, {nameof(SourceDocumentOrigin.ManualUpload)},
                         {nameof(SourceDocumentStatus.Acquired)}, 0, 0, TRUE, {OctoberNow}, {OctoberNow},
                         {OctoberNow.AddMinutes(1)}, {OctoberNow.AddMinutes(2)},
                         {"Syntetyczna decyzja sprzed migracji historii."})
                    """);

                await migrator.MigrateAsync();
                var backfilled = await migrationContext.SourceDocumentConflicts.AsNoTracking()
                    .SingleAsync(item => item.SourceDocumentId == legacyConflictId);
                Assert.Equal(OctoberNow.AddMinutes(2), backfilled.ResolvedAtUtc);
                Assert.Equal("Syntetyczna decyzja sprzed migracji historii.", backfilled.Resolution);
                await migrationContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""DELETE FROM "SourceDocuments" WHERE "Id" = {legacyConflictId}""");
            }

            await using (var conflictContext = CreateDbContext(connection))
            {
                var document = SourceDocument.CreateManual(Guid.NewGuid(), Owner, OctoberNow);
                document.MarkSourceConflict(new string('A', 64), OctoberNow.AddMinutes(1));
                document.ResolveSourceConflict(
                    "Syntetyczne wyjaśnienie konfliktu w teście PostgreSQL.",
                    OctoberNow.AddMinutes(2));
                conflictContext.SourceDocuments.Add(document);
                await conflictContext.SaveChangesAsync();
            }

            await using (var conflictVerification = CreateDbContext(connection))
            {
                var document = await conflictVerification.SourceDocuments.AsNoTracking().SingleAsync();
                Assert.False(document.HasUnresolvedSourceConflict);
                Assert.Equal(OctoberNow.AddMinutes(2), document.SourceConflictResolvedAtUtc);
                Assert.Equal(
                    "Syntetyczne wyjaśnienie konfliktu w teście PostgreSQL.",
                    document.SourceConflictResolution);
                var conflict = await conflictVerification.SourceDocumentConflicts.AsNoTracking().SingleAsync();
                Assert.Equal(OctoberNow.AddMinutes(2), conflict.ResolvedAtUtc);
                Assert.Equal(
                    "Syntetyczne wyjaśnienie konfliktu w teście PostgreSQL.",
                    conflict.Resolution);
            }

            var companyId = await SeedCompanyAndSaleAsync(connection);

            MonthClosingView?[] views;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                views = await Task.WhenAll(
                    CreateService(first).GetAsync(Owner, companyId, September, OctoberNow),
                    CreateService(second).GetAsync(Owner, companyId, September, OctoberNow));
            }

            var referenceId = Assert.IsType<Guid>(views[0]!.RuleSet?.Id);
            Assert.Equal(referenceId, views[1]!.RuleSet?.Id);

            var confirmation = new ConfirmCalculationRuleSetCommand(
                true,
                "synthetic:independent-postgres-review",
                new DateOnly(2026, 9, 12));
            CalculationRuleSetSnapshot[] confirmedRules;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                confirmedRules = await Task.WhenAll(
                    CreateService(first).ConfirmRuleSetAsync(
                        Owner, referenceId, confirmation, OctoberNow),
                    CreateService(second).ConfirmRuleSetAsync(
                        Owner, referenceId, confirmation, OctoberNow));
            }

            Assert.Equal(confirmedRules[0].Id, confirmedRules[1].Id);

            MonthDeclarationSnapshot[] declarations;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                declarations = await Task.WhenAll(
                    CreateService(first).SaveDeclarationAsync(
                        Owner, companyId, September, Declaration(), OctoberNow),
                    CreateService(second).SaveDeclarationAsync(
                        Owner, companyId, September, Declaration(), OctoberNow));
            }

            Assert.Equal(declarations[0].Id, declarations[1].Id);

            await InstallDeclarationRaceDelayAsync(connection);
            Attempt<MonthDeclarationSnapshot>[] competingDeclarations;
            try
            {
                await using var first = CreateDbContext(connection);
                await using var second = CreateDbContext(connection);
                competingDeclarations = await Task.WhenAll(
                    CaptureAsync(() => CreateService(first).SaveDeclarationAsync(
                        Owner, companyId, September, Declaration(pitAdjustment: 10m), OctoberNow.AddMinutes(1))),
                    CaptureAsync(() => CreateService(second).SaveDeclarationAsync(
                        Owner, companyId, September, Declaration(pitAdjustment: 20m), OctoberNow.AddMinutes(1))));
            }
            finally
            {
                await RemoveDeclarationRaceDelayAsync(connection);
            }

            Assert.Single(competingDeclarations, item => item.Value is not null);
            Assert.Single(competingDeclarations, item => item.Error is InvalidOperationException);

            await InstallSettlementRaceDelayAsync(connection);
            MonthSettlementSnapshot[] closes;
            try
            {
                await using var first = CreateDbContext(connection);
                await using var second = CreateDbContext(connection);
                closes = await Task.WhenAll(
                    CreateService(first).CloseAsync(Owner, companyId, September, OctoberNow),
                    CreateService(second).CloseAsync(Owner, companyId, September, OctoberNow));
            }
            finally
            {
                await RemoveSettlementRaceDelayAsync(connection);
            }

            Assert.Equal(closes[0].Id, closes[1].Id);

            await InstallSettlementRaceDelayAsync(connection);
            MonthSettlementSnapshot[] corrections;
            try
            {
                await using var first = CreateDbContext(connection);
                await using var second = CreateDbContext(connection);
                corrections = await Task.WhenAll(
                    CreateService(first).StartCorrectionAsync(
                        Owner, companyId, September, "syntetyczna korekta", OctoberNow.AddHours(1)),
                    CreateService(second).StartCorrectionAsync(
                        Owner, companyId, September, "syntetyczna korekta", OctoberNow.AddHours(1)));
            }
            finally
            {
                await RemoveSettlementRaceDelayAsync(connection);
            }

            Assert.Equal(corrections[0].Id, corrections[1].Id);
            await using (var verification = CreateDbContext(connection))
            {
                Assert.Equal(2, await verification.CalculationRuleSets.CountAsync());
                Assert.Equal(2, await verification.MonthDeclarations.CountAsync());
                Assert.Single(await verification.MonthCalculations.ToListAsync());
                Assert.Equal(2, await verification.MonthSettlements.CountAsync());
                Assert.Equal(
                    new[] { 1, 2 },
                    await verification.MonthSettlements
                        .OrderBy(item => item.VersionNumber)
                        .Select(item => item.VersionNumber)
                        .ToArrayAsync());
            }
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    [PostgresFact]
    public async Task Failed_close_rolls_back_calculation_and_settlement_together()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase7_rollback_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (var migrationContext = CreateDbContext(connection))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var companyId = await SeedCompanyAndSaleAsync(connection);
            await using (var setup = CreateDbContext(connection))
            {
                var service = CreateService(setup);
                var view = await service.GetAsync(Owner, companyId, September, OctoberNow);
                await service.ConfirmRuleSetAsync(
                    Owner,
                    view!.RuleSet!.Id,
                    new ConfirmCalculationRuleSetCommand(
                        true,
                        "synthetic:independent-postgres-review",
                        new DateOnly(2026, 9, 12)),
                    OctoberNow);
                await service.SaveDeclarationAsync(
                    Owner,
                    companyId,
                    September,
                    Declaration(),
                    OctoberNow);
            }

            await InstallFailingCloseAuditAsync(connection);
            try
            {
                await using var failing = CreateDbContext(connection);
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    CreateService(failing).CloseAsync(Owner, companyId, September, OctoberNow));
            }
            finally
            {
                await RemoveFailingCloseAuditAsync(connection);
            }

            await using (var verification = CreateDbContext(connection))
            {
                Assert.Empty(await verification.MonthCalculations.ToListAsync());
                Assert.Empty(await verification.MonthSettlements.ToListAsync());
            }

            await using (var retry = CreateDbContext(connection))
            {
                var closed = await CreateService(retry).CloseAsync(
                    Owner, companyId, September, OctoberNow.AddMinutes(1));
                Assert.Equal(MonthSettlementStatus.Closed, closed.Status);
            }

            await using (var verification = CreateDbContext(connection))
            {
                Assert.Single(await verification.MonthCalculations.ToListAsync());
                Assert.Single(await verification.MonthSettlements.ToListAsync());
            }
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    [PostgresFact]
    public async Task Concurrent_distinct_adjustments_cannot_jointly_reduce_a_total_below_zero()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase7_adjustment_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            await using (var migrationContext = CreateDbContext(connection))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var companyId = await SeedCompanyAndSaleAsync(connection);
            await InstallAdjustmentRaceDelayAsync(connection);
            Attempt<MonthAdjustmentSnapshot>[] attempts;
            try
            {
                await using var first = CreateDbContext(connection);
                await using var second = CreateDbContext(connection);
                attempts = await Task.WhenAll(
                    CaptureAsync(() => CreateService(first).AddAdjustmentAsync(
                        Owner,
                        companyId,
                        September,
                        new AddMonthAdjustmentCommand(
                            MonthAdjustmentKind.PitRevenue,
                            -6_000m,
                            "synthetic race A",
                            "synthetic:adjustment-race-a",
                            null,
                            null),
                        OctoberNow)),
                    CaptureAsync(() => CreateService(second).AddAdjustmentAsync(
                        Owner,
                        companyId,
                        September,
                        new AddMonthAdjustmentCommand(
                            MonthAdjustmentKind.PitRevenue,
                            -5_000m,
                            "synthetic race B",
                            "synthetic:adjustment-race-b",
                            null,
                            null),
                        OctoberNow)));
            }
            finally
            {
                await RemoveAdjustmentRaceDelayAsync(connection);
            }

            Assert.Single(attempts, item => item.Value is not null);
            Assert.Single(attempts, item => item.Error is InvalidOperationException);
            await using var verification = CreateDbContext(connection);
            var total = await verification.MonthTaxAdjustments.SumAsync(item => item.Amount);
            Assert.True(10_000m + total >= 0m);
            Assert.Single(await verification.MonthTaxAdjustments.ToListAsync());
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    [PostgresFact]
    public async Task Stale_source_conflict_write_cannot_disagree_with_the_append_only_history()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase7_conflict_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection)
        {
            Database = databaseName,
        }.ConnectionString;

        try
        {
            Guid documentId;
            await using (var seed = CreateDbContext(connection))
            {
                await seed.Database.MigrateAsync();
                var document = SourceDocument.CreateManual(Guid.NewGuid(), Owner, OctoberNow);
                document.MarkSourceConflict(new string('A', 64), OctoberNow.AddMinutes(1));
                seed.SourceDocuments.Add(document);
                await seed.SaveChangesAsync();
                documentId = document.Id;
            }

            await using var resolving = CreateDbContext(connection);
            await using var addingNewConflict = CreateDbContext(connection);
            var first = await resolving.SourceDocuments
                .Include(item => item.SourceConflicts)
                .SingleAsync(item => item.Id == documentId);
            var stale = await addingNewConflict.SourceDocuments
                .Include(item => item.SourceConflicts)
                .SingleAsync(item => item.Id == documentId);

            first.ResolveSourceConflict(
                "Syntetyczne rozwiązanie konfliktu A.",
                OctoberNow.AddMinutes(2));
            await resolving.SaveChangesAsync();

            stale.MarkSourceConflict(new string('B', 64), OctoberNow.AddMinutes(3));
            addingNewConflict.Entry(stale.SourceConflicts
                .Single(item => item.ConflictingSha256 == new string('B', 64)))
                .State = EntityState.Added;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                addingNewConflict.SaveChangesAsync());

            await using var verification = CreateDbContext(connection);
            var saved = await verification.SourceDocuments.AsNoTracking()
                .SingleAsync(item => item.Id == documentId);
            var history = await verification.SourceDocumentConflicts.AsNoTracking()
                .OrderBy(item => item.DetectedAtUtc)
                .ToListAsync();
            Assert.False(saved.HasUnresolvedSourceConflict);
            var onlyConflict = Assert.Single(history);
            Assert.Equal(new string('A', 64), onlyConflict.ConflictingSha256);
            Assert.Equal(OctoberNow.AddMinutes(2), onlyConflict.ResolvedAtUtc);
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static readonly string[] Phase7Tables =
    [
        "CalculationRuleSets",
        "MonthDeclarations",
        "MonthTaxAdjustments",
        "MonthCalculations",
        "MonthCalculationLines",
        "MonthSettlements",
        "SourceDocumentConflicts",
    ];

    private static AppDbContext CreateDbContext(string connectionString)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static MonthClosingService CreateService(AppDbContext context)
        => new(context, new AuditTrail(context));

    private static async Task<Guid> SeedCompanyAndSaleAsync(string connectionString)
    {
        await using var db = CreateDbContext(connectionString);
        var company = Company.Register(
            Owner,
            "Syntetyczna Firma Fazy 7",
            "1234563218",
            "Testowy adres 1, 00-001 Warszawa",
            September,
            "Syntetyczny Klient",
            "1234563218",
            "Testowy adres 2, 00-002 Warszawa",
            "Syntetyczna usługa",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            OctoberNow);
        db.Companies.Add(company);

        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            Owner,
            September,
            company.GetSubscriptionRate(September).Id,
            company.ServiceDescription,
            10_000m,
            23m,
            OctoberNow);
        invoice.PrepareForIssue(September.AddDays(1), "FV/TEST/09/2026", false, "<Invoice />", OctoberNow);
        invoice.RegisterSubmission("synthetic-session", "synthetic-submission", OctoberNow);
        invoice.MarkIssued("KSEF-SYNTHETIC", "<UPO />", "<Invoice />", OctoberNow);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static SaveMonthDeclarationCommand Declaration(decimal pitAdjustment = 0m)
        => new(
            SocialContributionsDeductible: 0m,
            PitBaseAdjustment: pitAdjustment,
            HealthIncomeAdjustment: 0m,
            OpeningVatCarryForward: 0m,
            OpeningPitAdvancesDue: 0m,
            OpeningBalancesConfirmed: true,
            HealthIncomeConfirmed: true,
            EvidenceReference: "synthetic:monthly-postgres-declaration");

    private static async Task<Attempt<T>> CaptureAsync<T>(Func<Task<T>> action)
        where T : class
    {
        try
        {
            return new Attempt<T>(await action(), null);
        }
        catch (Exception exception)
        {
            return new Attempt<T>(null, exception);
        }
    }

    private static async Task InstallDeclarationRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            CREATE OR REPLACE FUNCTION phase7_delay_declaration() RETURNS trigger AS $$
            BEGIN
                IF NEW."VersionNumber" = 2 THEN
                    PERFORM pg_sleep(0.5);
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase7_delay_declaration_trigger
                BEFORE INSERT ON "MonthDeclarations"
                FOR EACH ROW EXECUTE FUNCTION phase7_delay_declaration();
            """);

    private static async Task RemoveDeclarationRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            DROP TRIGGER IF EXISTS phase7_delay_declaration_trigger ON "MonthDeclarations";
            DROP FUNCTION IF EXISTS phase7_delay_declaration();
            """);

    private static async Task InstallSettlementRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            CREATE OR REPLACE FUNCTION phase7_delay_settlement() RETURNS trigger AS $$
            BEGIN
                PERFORM pg_sleep(0.5);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase7_delay_settlement_trigger
                BEFORE INSERT ON "MonthSettlements"
                FOR EACH ROW EXECUTE FUNCTION phase7_delay_settlement();
            """);

    private static async Task RemoveSettlementRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            DROP TRIGGER IF EXISTS phase7_delay_settlement_trigger ON "MonthSettlements";
            DROP FUNCTION IF EXISTS phase7_delay_settlement();
            """);

    private static async Task InstallAdjustmentRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            CREATE OR REPLACE FUNCTION phase7_delay_adjustment() RETURNS trigger AS $$
            BEGIN
                PERFORM pg_sleep(0.5);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase7_delay_adjustment_trigger
                BEFORE INSERT ON "MonthTaxAdjustments"
                FOR EACH ROW EXECUTE FUNCTION phase7_delay_adjustment();
            """);

    private static async Task RemoveAdjustmentRaceDelayAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            DROP TRIGGER IF EXISTS phase7_delay_adjustment_trigger ON "MonthTaxAdjustments";
            DROP FUNCTION IF EXISTS phase7_delay_adjustment();
            """);

    private static async Task InstallFailingCloseAuditAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            CREATE OR REPLACE FUNCTION phase7_fail_close_audit() RETURNS trigger AS $$
            BEGIN
                IF NEW."Action" = 'month-closed' THEN
                    RAISE EXCEPTION 'synthetic phase 7 close failure';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER phase7_fail_close_audit_trigger
                BEFORE INSERT ON "AuditEvents"
                FOR EACH ROW EXECUTE FUNCTION phase7_fail_close_audit();
            """);

    private static async Task RemoveFailingCloseAuditAsync(string connectionString)
        => await ExecuteSqlAsync(
            connectionString,
            """
            DROP TRIGGER IF EXISTS phase7_fail_close_audit_trigger ON "AuditEvents";
            DROP FUNCTION IF EXISTS phase7_fail_close_audit();
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
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @table
                  AND column_name = @column)
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
            $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record Attempt<T>(T? Value, Exception? Error)
        where T : class;
}
