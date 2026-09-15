using Firemka.Application.Onboarding;
using Firemka.Application.Jobs;
using Firemka.Application.Documents;
using Firemka.Application.Sales;
using Firemka.Domain.Companies;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Companies;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Firemka.Infrastructure.Tests;

public sealed class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task Migration_round_trip_and_concurrent_job_guards_work_on_postgres()
    {
        var administrativeConnection = Environment.GetEnvironmentVariable("FIREMKA_TEST_POSTGRES")!;
        var databaseName = $"firemka_phase2_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(administrativeConnection, databaseName);

        try
        {
            var databaseConnection = new NpgsqlConnectionStringBuilder(administrativeConnection)
            {
                Database = databaseName,
            }.ConnectionString;
            await using var migrationContext = CreateDbContext(databaseConnection);
            var migrator = migrationContext.GetService<IMigrator>();

            await migrator.MigrateAsync();
            Assert.Equal("BackgroundJobs", await GetTableNameAsync(databaseConnection, "BackgroundJobs"));
            Assert.Equal("Companies", await GetTableNameAsync(databaseConnection, "Companies"));
            Assert.Equal("KsefSyncRuns", await GetTableNameAsync(databaseConnection, "KsefSyncRuns"));
            Assert.Equal("DocumentExtractionAttempts", await GetTableNameAsync(databaseConnection, "DocumentExtractionAttempts"));
            Assert.Equal("SalesInvoices", await GetTableNameAsync(databaseConnection, "SalesInvoices"));
            Assert.Equal("SalesAutomationSettings", await GetTableNameAsync(databaseConnection, "SalesAutomationSettings"));
            Assert.True(await ColumnExistsAsync(databaseConnection, "StoredFiles", "Origin"));
            Assert.True(await ColumnExistsAsync(databaseConnection, "SourceDocuments", "SourceSha256"));

            await migrator.MigrateAsync("20260910075958_CoreDataFilesAndJobs");
            Assert.Equal("StoredFiles", await GetTableNameAsync(databaseConnection, "StoredFiles"));
            Assert.Null(await GetTableNameAsync(databaseConnection, "Companies"));
            Assert.Null(await GetTableNameAsync(databaseConnection, "KsefSyncRuns"));
            Assert.Null(await GetTableNameAsync(databaseConnection, "SalesInvoices"));
            Assert.False(await ColumnExistsAsync(databaseConnection, "StoredFiles", "Origin"));

            await migrator.MigrateAsync("20260909170447_InitialIdentity");
            Assert.Null(await GetTableNameAsync(databaseConnection, "BackgroundJobs"));
            Assert.Equal("AspNetUsers", await GetTableNameAsync(databaseConnection, "AspNetUsers"));

            await migrator.MigrateAsync();
            await using (var companyContext = CreateDbContext(databaseConnection))
            {
                var profiles = new CompanyProfileService(companyContext);
                await profiles.RegisterAsync(new RegisterCompanyCommand(
                    "owner-postgres",
                    "Testowa Firma",
                    "1234563218",
                    "Testowy adres firmy 2, 00-002 Warszawa",
                    new DateOnly(2026, 10, 15),
                    "Testowy Klient",
                    "1234563218",
                    "Testowy adres 1, 00-001 Warszawa",
                    "Miesięczny dostęp do aplikacji Test SaaS",
                    1_500m,
                    23m,
                    0.91m,
                    VehicleArrangement.None,
                    DateTimeOffset.Parse("2026-09-10T10:00:00Z")));
                await profiles.ChangeSubscriptionRateAsync(
                    "owner-postgres",
                    new DateOnly(2027, 1, 1),
                    1_800m,
                    23m,
                    DateTimeOffset.Parse("2026-12-20T10:00:00Z"));
                await profiles.ChangeVatProfileAsync(
                    "owner-postgres",
                    new DateOnly(2027, 1, 1),
                    VatProfile.VatExempt,
                    DateTimeOffset.Parse("2026-12-20T10:00:00Z"));
            }

            await using (var readCompanyContext = CreateDbContext(databaseConnection))
            {
                var profile = await new CompanyProfileService(readCompanyContext).GetAsync("owner-postgres");
                Assert.NotNull(profile);
                Assert.Equal("Testowy adres firmy 2, 00-002 Warszawa", profile.CompanyAddress);
                Assert.Equal("Miesięczny dostęp do aplikacji Test SaaS", profile.ServiceDescription);
                Assert.Collection(
                    profile.SubscriptionRates,
                    item => Assert.Equal(1_500m, item.NetMonthlyAmount),
                    item => Assert.Equal(1_800m, item.NetMonthlyAmount));
                Assert.Collection(
                    profile.VatProfiles,
                    item => Assert.Equal(VatProfile.ActiveMonthly, item.Profile),
                    item => Assert.Equal(VatProfile.VatExempt, item.Profile));
            }

            Guid salesInvoiceId;
            var salesNow = DateTimeOffset.Parse("2026-11-01T08:00:00Z");
            await using (var firstDraftContext = CreateDbContext(databaseConnection))
            await using (var secondDraftContext = CreateDbContext(databaseConnection))
            {
                var draftResults = await Task.WhenAll(
                    CreateSalesService(firstDraftContext, new BlockingOutgoingGateway(block: false))
                        .EnsureDraftAsync("owner-postgres", new DateOnly(2026, 11, 1), salesNow),
                    CreateSalesService(secondDraftContext, new BlockingOutgoingGateway(block: false))
                        .EnsureDraftAsync("owner-postgres", new DateOnly(2026, 11, 1), salesNow));
                Assert.Equal(draftResults[0].Id, draftResults[1].Id);
                salesInvoiceId = draftResults[0].Id;
            }

            await using (var draftVerificationContext = CreateDbContext(databaseConnection))
            {
                Assert.Equal(
                    1,
                    await draftVerificationContext.SalesInvoices.CountAsync(
                        item => item.ServiceMonth == new DateOnly(2026, 11, 1)));
            }

            var outgoing = new BlockingOutgoingGateway(block: true);
            await using var firstSalesContext = CreateDbContext(databaseConnection);
            await using var secondSalesContext = CreateDbContext(databaseConnection);
            var firstIssue = CreateSalesService(firstSalesContext, outgoing).IssueAsync(
                "owner-postgres",
                salesInvoiceId,
                automatic: false,
                new DateOnly(2026, 11, 1),
                salesNow);
            await outgoing.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var secondIssue = await CreateSalesService(secondSalesContext, outgoing).IssueAsync(
                "owner-postgres",
                salesInvoiceId,
                automatic: false,
                new DateOnly(2026, 11, 1),
                salesNow);
            outgoing.ReleaseSend.TrySetResult();
            await firstIssue;
            Assert.Equal(Firemka.Domain.Sales.SalesInvoiceStatus.Sending, secondIssue.Status);
            Assert.Equal(1, outgoing.SendCalls);

            Guid rejectedInvoiceId;
            await using (var rejectedContext = CreateDbContext(databaseConnection))
            {
                var rejectedService = CreateSalesService(rejectedContext, new RejectingOutgoingGateway());
                var rejectedDraft = await rejectedService.EnsureDraftAsync(
                    "owner-postgres",
                    new DateOnly(2026, 12, 1),
                    salesNow);
                rejectedInvoiceId = rejectedDraft.Id;
                var rejected = await rejectedService.IssueAsync(
                    "owner-postgres",
                    rejectedInvoiceId,
                    automatic: false,
                    new DateOnly(2026, 12, 1),
                    salesNow);
                Assert.Equal(Firemka.Domain.Sales.SalesInvoiceStatus.Rejected, rejected.Status);
            }

            await using (var firstReopenContext = CreateDbContext(databaseConnection))
            await using (var secondReopenContext = CreateDbContext(databaseConnection))
            {
                await Task.WhenAll(
                    CreateSalesService(firstReopenContext, new BlockingOutgoingGateway(block: false))
                        .ReopenRejectedAsync("owner-postgres", rejectedInvoiceId, salesNow.AddMinutes(2)),
                    CreateSalesService(secondReopenContext, new BlockingOutgoingGateway(block: false))
                        .ReopenRejectedAsync("owner-postgres", rejectedInvoiceId, salesNow.AddMinutes(2)));
            }

            await using (var reopenedVerificationContext = CreateDbContext(databaseConnection))
            {
                Assert.Equal(
                    Firemka.Domain.Sales.SalesInvoiceStatus.Draft,
                    (await reopenedVerificationContext.SalesInvoices
                        .SingleAsync(item => item.Id == rejectedInvoiceId)).Status);
                Assert.Single(await reopenedVerificationContext.InvoiceVersions
                    .Where(item => item.InvoiceId == rejectedInvoiceId)
                    .ToListAsync());
            }

            var acceptedStatus = new ConcurrentAcceptedGateway(Assert.Single(outgoing.SentXml));
            await using (var firstStatusContext = CreateDbContext(databaseConnection))
            await using (var secondStatusContext = CreateDbContext(databaseConnection))
            {
                var firstStatus = CreateSalesService(firstStatusContext, acceptedStatus).IssueAsync(
                    "owner-postgres",
                    salesInvoiceId,
                    automatic: false,
                    new DateOnly(2026, 11, 1),
                    salesNow.AddMinutes(1));
                var secondStatus = CreateSalesService(secondStatusContext, acceptedStatus).IssueAsync(
                    "owner-postgres",
                    salesInvoiceId,
                    automatic: false,
                    new DateOnly(2026, 11, 1),
                    salesNow.AddMinutes(1));
                await acceptedStatus.BothStatusChecksEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                acceptedStatus.ReleaseStatusChecks.TrySetResult();

                var finalResults = await Task.WhenAll(firstStatus, secondStatus);
                Assert.All(finalResults, item => Assert.Equal(
                    Firemka.Domain.Sales.SalesInvoiceStatus.Issued,
                    item.Status));
            }

            await using (var finalSalesContext = CreateDbContext(databaseConnection))
            {
                Assert.Equal(
                    Firemka.Domain.Sales.SalesInvoiceStatus.Issued,
                    (await finalSalesContext.SalesInvoices.SingleAsync(item => item.Id == salesInvoiceId)).Status);
                Assert.Single(await finalSalesContext.InvoiceVersions
                    .Where(item => item.InvoiceId == salesInvoiceId)
                    .ToListAsync());
            }
            Assert.Equal(1, outgoing.SendCalls);

            var failedUploadRoot = Path.Combine(
                Path.GetTempPath(),
                $"firemka-postgres-upload-{Guid.NewGuid():N}");
            try
            {
                await using (var failedUploadContext = CreateDbContext(databaseConnection))
                {
                    var files = new StoredFileService(
                        failedUploadContext,
                        new PrivateFileStore(new PrivateFileStoreOptions
                        {
                            RootPath = failedUploadRoot,
                            MaximumFileSizeBytes = 1_024,
                        }));
                    var incoming = new IncomingDocumentService(
                        failedUploadContext,
                        files,
                        new ThrowingQueue(),
                        new AuditTrail(failedUploadContext));

                    await Assert.ThrowsAsync<InvalidOperationException>(() => incoming.UploadAsync(
                        "owner-postgres",
                        new IncomingDocumentUpload(
                            "scan.png",
                            "image/png",
                            new MemoryStream([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x01])),
                        DateTimeOffset.UtcNow));
                }

                await using var failedUploadVerification = CreateDbContext(databaseConnection);
                Assert.Empty(await failedUploadVerification.SourceDocuments.ToListAsync());
                Assert.Empty(await failedUploadVerification.StoredFiles.ToListAsync());
                Assert.DoesNotContain(
                    await failedUploadVerification.AuditEvents.ToListAsync(),
                    item => item.EntityType == nameof(Firemka.Domain.Documents.SourceDocument));
                Assert.Empty(Directory.Exists(failedUploadRoot)
                    ? Directory.GetFiles(failedUploadRoot, "*", SearchOption.AllDirectories)
                    : []);
            }
            finally
            {
                if (Directory.Exists(failedUploadRoot))
                {
                    Directory.Delete(failedUploadRoot, recursive: true);
                }
            }

            var now = DateTimeOffset.Parse("2026-09-10T08:00:00Z");
            var command = new BackgroundJobCommand(
                "test.concurrent",
                "{}",
                "test.concurrent:1",
                now);

            await using var firstEnqueueContext = CreateDbContext(databaseConnection);
            await using var secondEnqueueContext = CreateDbContext(databaseConnection);
            var enqueueResults = await Task.WhenAll(
                new BackgroundJobQueue(firstEnqueueContext).EnqueueAsync(command),
                new BackgroundJobQueue(secondEnqueueContext).EnqueueAsync(command));
            Assert.Equal(enqueueResults[0], enqueueResults[1]);

            await using var firstLeaseContext = CreateDbContext(databaseConnection);
            await using var secondLeaseContext = CreateDbContext(databaseConnection);
            var leaseResults = await Task.WhenAll(
                new BackgroundJobQueue(firstLeaseContext).TryLeaseAsync(
                    "worker-a",
                    now,
                    TimeSpan.FromMinutes(1)),
                new BackgroundJobQueue(secondLeaseContext).TryLeaseAsync(
                    "worker-b",
                    now,
                    TimeSpan.FromMinutes(1)));
            var firstLease = Assert.Single(leaseResults, result => result is not null)!;

            await using var renewalContext = CreateDbContext(databaseConnection);
            Assert.True(await new BackgroundJobQueue(renewalContext).RenewLeaseAsync(
                firstLease.Id,
                leaseResults[0] is not null ? "worker-a" : "worker-b",
                now.AddSeconds(30),
                TimeSpan.FromMinutes(1)));
            await using var activeLeaseContext = CreateDbContext(databaseConnection);
            Assert.Null(await new BackgroundJobQueue(activeLeaseContext).TryLeaseAsync(
                "worker-too-early",
                now.AddMinutes(1).AddSeconds(1),
                TimeSpan.FromMinutes(1)));

            await using var recoveryContext = CreateDbContext(databaseConnection);
            var recoveredLease = await new BackgroundJobQueue(recoveryContext).TryLeaseAsync(
                "worker-after-crash",
                now.AddMinutes(1).AddSeconds(31),
                TimeSpan.FromMinutes(1));
            Assert.NotNull(recoveredLease);
            Assert.Equal(firstLease.Id, recoveredLease.Id);
            Assert.Equal(2, recoveredLease.AttemptNumber);
        }
        finally
        {
            await DropDatabaseAsync(administrativeConnection, databaseName);
        }
    }

    private static AppDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static SalesInvoiceService CreateSalesService(
        AppDbContext dbContext,
        IOutgoingKsefGateway gateway)
        => new(
            dbContext,
            gateway,
            new Fa3InvoiceGenerator(),
            new AuditTrail(dbContext),
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = false });

    private static async Task<string?> GetTableNameAsync(
        string connectionString,
        string tableName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            select case
                when to_regclass('public."' || @table_name || '"') is null then null
                else @table_name
            end
            """,
            connection);
        command.Parameters.AddWithValue("table_name", tableName);
        return await command.ExecuteScalarAsync() as string;
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
            select exists (
                select 1
                from information_schema.columns
                where table_schema = 'public'
                  and table_name = @table_name
                  and column_name = @column_name)
            """,
            connection);
        command.Parameters.AddWithValue("table_name", tableName);
        command.Parameters.AddWithValue("column_name", columnName);
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

    private sealed class ThrowingQueue : IBackgroundJobQueue
    {
        public Task<Guid> EnqueueAsync(
            BackgroundJobCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromException<Guid>(new InvalidOperationException("Kontrolowana awaria kolejki."));

        public Task<LeasedBackgroundJob?> TryLeaseAsync(
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> RenewLeaseAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task CompleteAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task FailAsync(
            Guid jobId,
            string workerId,
            string error,
            DateTimeOffset failedAtUtc,
            TimeSpan retryDelay,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BlockingOutgoingGateway(bool block) : IOutgoingKsefGateway
    {
        public int SendCalls { get; private set; }
        public List<string> SentXml { get; } = [];
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<OutgoingKsefSubmission> SendAsync(
            OutgoingKsefInvoice invoice,
            CancellationToken cancellationToken = default)
        {
            SendCalls++;
            SentXml.Add(invoice.Fa3Xml);
            SendEntered.TrySetResult();
            if (block)
            {
                await ReleaseSend.Task.WaitAsync(cancellationToken);
            }

            return new OutgoingKsefSubmission("SESSION-PG", "SUBMISSION-PG");
        }

        public Task<OutgoingKsefStatus> GetStatusAsync(
            string idempotencyKey,
            string? sessionReferenceNumber,
            string? submissionReferenceNumber,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new OutgoingKsefStatus(OutgoingKsefProcessingState.Pending));
    }

    private sealed class ConcurrentAcceptedGateway(string returnedInvoiceXml) : IOutgoingKsefGateway
    {
        private int _statusCalls;

        public TaskCompletionSource BothStatusChecksEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStatusChecks { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OutgoingKsefSubmission> SendAsync(
            OutgoingKsefInvoice invoice,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sprawdzanie statusu nie może ponownie wysłać faktury.");

        public async Task<OutgoingKsefStatus> GetStatusAsync(
            string idempotencyKey,
            string? sessionReferenceNumber,
            string? submissionReferenceNumber,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _statusCalls) == 2)
            {
                BothStatusChecksEntered.TrySetResult();
            }

            await ReleaseStatusChecks.Task.WaitAsync(cancellationToken);
            return new OutgoingKsefStatus(
                OutgoingKsefProcessingState.Accepted,
                sessionReferenceNumber,
                submissionReferenceNumber,
                "1234563218-20261101-ABCDEF-ABCDEF-01",
                "<UPO>synthetic</UPO>",
                returnedInvoiceXml);
        }
    }

    private sealed class RejectingOutgoingGateway : IOutgoingKsefGateway
    {
        public Task<OutgoingKsefSubmission> SendAsync(
            OutgoingKsefInvoice invoice,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OutgoingKsefSubmission("SESSION-REJECTED", "SUBMISSION-REJECTED"));

        public Task<OutgoingKsefStatus> GetStatusAsync(
            string idempotencyKey,
            string? sessionReferenceNumber,
            string? submissionReferenceNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OutgoingKsefStatus(
                OutgoingKsefProcessingState.Rejected,
                sessionReferenceNumber,
                submissionReferenceNumber,
                RejectionReason: "Kontrolowane odrzucenie testowe."));
    }
}
