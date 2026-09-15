using Firemka.Application.Payments;
using Firemka.Domain.Companies;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Email;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Notifications;
using Firemka.Infrastructure.Payments;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class Phase10PostgresTests
{
    private const string PreviousMigration = "20260914102849_Phase9AnnualClosing";
    private const string Owner = "owner-phase10-postgres";
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-09T06:05:00Z");

    [PostgresFact]
    public async Task Migration_and_forced_races_keep_one_payment_and_one_notification()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase10_{Guid.NewGuid():N}";
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
                Assert.True(await TableExistsAsync(connection, "Payments"));
                Assert.True(await TableExistsAsync(connection, "Notifications"));
                var migrator = migration.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration);
                Assert.False(await TableExistsAsync(connection, "Payments"));
                Assert.False(await TableExistsAsync(connection, "Notifications"));
                await migrator.MigrateAsync();
            }

            var companyId = await SeedIssuedInvoiceAsync(connection);

            await InstallInsertDelayAsync(connection, "Notifications", "phase10_delay_notification");
            try
            {
                var options = new EmailOptions
                {
                    Enabled = true,
                    Recipient = "owner@example.test",
                    BaseUrl = "https://firemka.example.test",
                };
                await using var first = CreateDb(connection);
                await using var second = CreateDb(connection);
                var created = await Task.WhenAll(
                    new NotificationScheduler(first, new PaymentService(first, new AuditTrail(first)),
                        new TransactionalOutbox(first), options).EnqueueDueAsync(Now),
                    new NotificationScheduler(second, new PaymentService(second, new AuditTrail(second)),
                        new TransactionalOutbox(second), options).EnqueueDueAsync(Now));
                Assert.Equal(1, created.Sum());
            }
            finally
            {
                await RemoveInsertDelayAsync(connection, "Notifications", "phase10_delay_notification");
            }

            PaymentObligationSnapshot obligation;
            await using (var db = CreateDb(connection))
            {
                obligation = Assert.Single(
                    (await new PaymentService(db, new AuditTrail(db)).GetAsync(Owner, companyId, September))
                    .Obligations,
                    item => item.Kind == PaymentKind.ClientInvoice);
            }
            var command = new RecordFullPaymentCommand(
                obligation.Kind, obligation.TargetId, obligation.TargetVersion, obligation.AmountDue,
                DateOnly.FromDateTime(Now.Date), "bank-reference-phase10");

            await InstallInsertDelayAsync(connection, "Payments", "phase10_delay_payment");
            try
            {
                await using var first = CreateDb(connection);
                await using var second = CreateDb(connection);
                var results = await Task.WhenAll(
                    new PaymentService(first, new AuditTrail(first)).RecordFullAsync(
                        Owner, companyId, September, command, Now),
                    new PaymentService(second, new AuditTrail(second)).RecordFullAsync(
                        Owner, companyId, September, command, Now));
                Assert.Equal(results[0].Id, results[1].Id);
            }
            finally
            {
                await RemoveInsertDelayAsync(connection, "Payments", "phase10_delay_payment");
            }

            await using (var verification = CreateDb(connection))
            {
                Assert.Single(await verification.Payments.AsNoTracking().ToListAsync());
                Assert.Single(await verification.Notifications.AsNoTracking().ToListAsync());
                Assert.Single(await verification.OutboxMessages.AsNoTracking().ToListAsync());
                Assert.Single(await verification.AuditEvents.AsNoTracking()
                    .Where(item => item.Action == "full-payment-recorded").ToListAsync());
            }
        }
        finally
        {
            await RemoveInsertDelayAsync(connection, "Payments", "phase10_delay_payment");
            await RemoveInsertDelayAsync(connection, "Notifications", "phase10_delay_notification");
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static async Task<Guid> SeedIssuedInvoiceAsync(string connection)
    {
        await using var db = CreateDb(connection);
        var company = Company.Register(
            Owner, "Syntetyczna Firma Fazy 10", "1010000000", "Adres firmy", September,
            "Syntetyczny Klient", "1234567890", "Adres klienta", "Usługa testowa",
            1_000m, 23m, 1m, VehicleArrangement.None, Now);
        var invoice = SalesInvoice.CreateDraft(
            company.Id, Owner, September, company.GetSubscriptionRate(September).Id,
            company.ServiceDescription, 1_000m, 23m, Now);
        invoice.PrepareForIssue(September, "FV/09/2026", false, "<Invoice />", Now);
        invoice.RegisterSubmission("session-phase10", "submission-phase10", Now);
        invoice.MarkIssued("1010000000-20260901-000000000000-00", "<UPO />", "<Invoice />", Now);
        db.Companies.Add(company);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static AppDbContext CreateDb(string connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static Task InstallInsertDelayAsync(string connection, string table, string function)
        => ExecuteSqlAsync(connection,
            $"""
            CREATE OR REPLACE FUNCTION {function}() RETURNS trigger AS $$
            BEGIN
                PERFORM pg_sleep(0.4);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER {function}_trigger BEFORE INSERT ON "{table}"
                FOR EACH ROW EXECUTE FUNCTION {function}();
            """);

    private static Task RemoveInsertDelayAsync(string connection, string table, string function)
        => ExecuteSqlAsync(connection,
            $"""
            DROP TRIGGER IF EXISTS {function}_trigger ON "{table}";
            DROP FUNCTION IF EXISTS {function}();
            """);

    private static async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
        }
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
