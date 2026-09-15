using System.Text;
using Firemka.Application.Accounting;
using Firemka.Application.Charging;
using Firemka.Application.Vehicles;
using Firemka.Domain.Accounting;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Accounting;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Charging;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase6PostgresTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T10:00:00Z");

    [PostgresFact]
    public async Task Migration_round_trip_and_parallel_replays_remain_single_on_postgres()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase6_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);
        var connection = new NpgsqlConnectionStringBuilder(administrativeConnection) { Database = databaseName }.ConnectionString;

        try
        {
            await using (var migrationContext = CreateDbContext(connection))
            {
                await migrationContext.Database.MigrateAsync();
            }

            Assert.True(await TableExistsAsync(connection, "CostRules"));
            Assert.True(await TableExistsAsync(connection, "VehicleCostPolicies"));
            Assert.True(await TableExistsAsync(connection, "ChargingSessions"));

            Guid companyId;
            Guid firstDocumentId;
            await using (var seed = CreateDbContext(connection))
            {
                var company = CreateCompany();
                seed.Companies.Add(company);
                var (document, storedFile) = CreateDocument("FV/1", 123m);
                seed.StoredFiles.Add(storedFile);
                seed.SourceDocuments.Add(document);
                await seed.SaveChangesAsync();
                companyId = company.Id;
                firstDocumentId = document.Id;
            }

            CostBookingSnapshot[] prepared;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                prepared = await Task.WhenAll(
                    CreateAccounting(first).PrepareAsync("owner-postgres", firstDocumentId, CostInput(123m, 23m), Now),
                    CreateAccounting(second).PrepareAsync("owner-postgres", firstDocumentId, CostInput(123m, 23m), Now));
            }

            Assert.Equal(prepared[0].Id, prepared[1].Id);
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                await Task.WhenAll(
                    CreateAccounting(first).ConfirmAndApplyFutureAsync("owner-postgres", prepared[0].Id, CostDecision(), Now),
                    CreateAccounting(second).ConfirmAndApplyFutureAsync("owner-postgres", prepared[0].Id, CostDecision(), Now));
            }

            await using (var verification = CreateDbContext(connection))
            {
                Assert.Single(await verification.CostBookings.ToListAsync());
                Assert.Single(await verification.CostRules.ToListAsync());
                Assert.Single(await verification.KpirEntries.ToListAsync());
                Assert.Single(await verification.VatPurchaseEntries.ToListAsync());
            }

            Guid secondDocumentId;
            await using (var seed = CreateDbContext(connection))
            {
                var (document, storedFile) = CreateDocument("FV/2", 246m);
                seed.StoredFiles.Add(storedFile);
                seed.SourceDocuments.Add(document);
                await seed.SaveChangesAsync();
                secondDocumentId = document.Id;
            }

            await using (var automatic = CreateDbContext(connection))
            {
                var result = await CreateAccounting(automatic).PrepareAsync(
                    "owner-postgres", secondDocumentId, CostInput(246m, 46m), Now.AddDays(1));
                Assert.Equal(CostBookingStatus.BookedAutomatically, result.Status);
            }

            VehiclePolicySnapshot[] vehiclePolicies;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                var command = new CreateVehiclePolicyCommand(
                    companyId, VehicleCostKind.Operation, new DateOnly(2026, 9, 1));
                vehiclePolicies = await Task.WhenAll(
                    new VehiclePolicyService(first).CreatePendingAsync("owner-postgres", command, Now),
                    new VehiclePolicyService(second).CreatePendingAsync("owner-postgres", command, Now));
            }

            Assert.Equal(vehiclePolicies[0].Id, vehiclePolicies[1].Id);
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                var activation = new ActivateVehiclePolicyCommand(50m, 75m, "syntetyczny dowód testowy");
                var activated = await Task.WhenAll(
                    new VehiclePolicyService(first).ActivateAsync(
                        "owner-postgres", vehiclePolicies[0].Id, activation, Now),
                    new VehiclePolicyService(second).ActivateAsync(
                        "owner-postgres", vehiclePolicies[0].Id, activation, Now));
                Assert.All(activated, item => Assert.Equal(VehiclePolicyStatus.Active, item.Status));
            }

            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                var revisions = await Task.WhenAll(
                    new VehiclePolicyService(first).CreateRevisionAsync(
                        "owner-postgres", vehiclePolicies[0].Id, new DateOnly(2026, 10, 1), Now.AddDays(1)),
                    new VehiclePolicyService(second).CreateRevisionAsync(
                        "owner-postgres", vehiclePolicies[0].Id, new DateOnly(2026, 10, 1), Now.AddDays(1)));
                Assert.Equal(revisions[0].Id, revisions[1].Id);
            }

            Guid profileId;
            await using (var setup = CreateDbContext(connection))
            {
                var charging = new HomeChargingService(setup, new ChargingCsvParser());
                var profile = await charging.SaveProfileAsync(
                    "owner-postgres",
                    companyId,
                    new SaveChargingProfileCommand(';', "started", "energy", "yyyy-MM-dd HH:mm", "Europe/Warsaw", "id"),
                    Now);
                profile = await charging.ActivateProfileAsync("owner-postgres", profile.Id, true, Now);
                profileId = profile.Id;
            }

            var bytes = Encoding.UTF8.GetBytes("started;energy;id\n2026-09-01 01:00;10000;A");
            ChargingImportSnapshot[] imports;
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                imports = await Task.WhenAll(
                    new HomeChargingService(first, new ChargingCsvParser()).ImportAsync(
                        "owner-postgres", companyId, profileId, new MemoryStream(bytes), Now),
                    new HomeChargingService(second, new ChargingCsvParser()).ImportAsync(
                        "owner-postgres", companyId, profileId, new MemoryStream(bytes), Now));
            }

            Assert.Equal(imports[0].BatchId, imports[1].BatchId);
            var firstOverlap = Encoding.UTF8.GetBytes(
                "started;energy;id\n2026-09-02 01:00;1000;D\n2026-09-02 02:00;1000;E");
            var secondOverlap = Encoding.UTF8.GetBytes(
                "started;energy;id\n2026-09-02 01:00;1000;D\n2026-09-02 03:00;1000;F");
            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                var overlaps = await Task.WhenAll(
                    new HomeChargingService(first, new ChargingCsvParser()).ImportAsync(
                        "owner-postgres", companyId, profileId, new MemoryStream(firstOverlap), Now.AddMinutes(1)),
                    new HomeChargingService(second, new ChargingCsvParser()).ImportAsync(
                        "owner-postgres", companyId, profileId, new MemoryStream(secondOverlap), Now.AddMinutes(1)));
                Assert.Equal(3, overlaps.Sum(item => item.AddedRows));
                Assert.All(overlaps, item => Assert.False(item.IsReplay));
            }

            await using (var first = CreateDbContext(connection))
            await using (var second = CreateDbContext(connection))
            {
                var reports = await Task.WhenAll(
                    new HomeChargingService(first, new ChargingCsvParser()).GenerateReportAsync(
                        "owner-postgres", companyId, new DateOnly(2026, 9, 1), Now),
                    new HomeChargingService(second, new ChargingCsvParser()).GenerateReportAsync(
                        "owner-postgres", companyId, new DateOnly(2026, 9, 1), Now));
                Assert.Equal(reports[0].ReportId, reports[1].ReportId);
            }

            await using (var verification = CreateDbContext(connection))
            {
                Assert.Equal(3, await verification.ChargingImportBatches.CountAsync());
                Assert.Equal(4, await verification.ChargingSessions.CountAsync());
                Assert.Single(await verification.ChargingReports.ToListAsync());
                Assert.Equal(2, await verification.VehicleCostPolicies.CountAsync());
                Assert.Equal(2, await verification.CostBookings.CountAsync());
                Assert.Equal(2, await verification.KpirEntries.CountAsync());
                Assert.Equal(2, await verification.VatPurchaseEntries.CountAsync());
            }

            await using (var migrationContext = CreateDbContext(connection))
            {
                var migrator = migrationContext.GetService<IMigrator>();
                await migrator.MigrateAsync("20260911135310_SalesInvoicesAndOutgoingKsef");
                Assert.False(await TableExistsAsync(connection, "CostRules"));
                Assert.False(await TableExistsAsync(connection, "ChargingSessions"));
                await migrator.MigrateAsync();
                Assert.True(await TableExistsAsync(connection, "CostRules"));
                Assert.True(await TableExistsAsync(connection, "ChargingSessions"));
            }
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static AppDbContext CreateDbContext(string connectionString)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static CostAccountingService CreateAccounting(AppDbContext context)
        => new(context, new AuditTrail(context));

    private static Company CreateCompany()
        => Company.Register(
            "owner-postgres", "Testowa Firma", "1234563218", "Testowy adres", new DateOnly(2026, 1, 1),
            "Testowy Klient", "1234563218", "Adres klienta", "Testowa usługa",
            1_500m, 23m, 0.91m, VehicleArrangement.None, Now);

    private static (SourceDocument Document, StoredFile StoredFile) CreateDocument(string number, decimal gross)
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-postgres", Now);
        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "owner-postgres",
            StorageKey = $"phase6/{Guid.NewGuid():N}",
            OriginalFileName = $"{number}.pdf",
            MediaType = "application/pdf",
            SizeBytes = 1,
            Sha256 = new string('A', 64),
            Origin = StoredFileOrigin.ManualUpload,
            RecordType = StoredFileRecordType.SourceDocument,
            RecordId = document.Id,
            RecordVersion = 1,
            CreatedAtUtc = Now,
        };
        document.AttachSource(file.Id, file.Sha256, Now);
        document.ApplyExtraction(DocumentData.Empty, null, Now);
        document.ConfirmData(new DocumentData(
            number, "Testowy Dostawca", "PL123", new DateOnly(2026, 9, 1), gross, "PLN"), Now);
        return (document, file);
    }

    private static PrepareCostCommand CostInput(decimal gross, decimal inputVat)
        => new("PL", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai, gross, inputVat);

    private static CostDecisionCommand CostDecision()
        => new(
            "Pozostałe wydatki", 50m, 75m,
            AccountingPeriodPolicy.IssueMonth, AccountingPeriodPolicy.IssueMonth,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), "sztuczna decyzja");

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
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
