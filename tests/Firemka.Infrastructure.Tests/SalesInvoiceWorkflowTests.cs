using System.Xml.Linq;
using System.Text.Json;
using Firemka.Application.Jobs;
using Firemka.Application.Sales;
using Firemka.Domain.Companies;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Sales;
using Firemka.Infrastructure.Companies;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class SalesInvoiceWorkflowTests
{
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse("2026-10-02T08:00:00Z");

    [Fact]
    public async Task Ensuring_the_same_month_twice_keeps_one_draft()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var service = CreateService(db, new FakeOutgoingKsefGateway());

        var first = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);
        var second = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 20), NowUtc);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.SalesInvoices.ToListAsync());
    }

    [Fact]
    public async Task Late_approval_does_not_remove_the_separate_current_month_invoice()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.Accepting();
        var service = CreateService(db, gateway);
        var september = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 9, 1), NowUtc);
        await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var result = await service.IssueAsync(
            "owner-1", september.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);

        Assert.True(result.IsLateApproval);
        Assert.Equal(SalesInvoiceStatus.Issued, result.Status);
        var invoices = await service.ListAsync("owner-1");
        Assert.Equal(2, invoices.Count);
        Assert.Contains(invoices, item => item.ServiceMonth == new DateOnly(2026, 10, 1)
            && item.Status == SalesInvoiceStatus.Draft);
    }

    [Fact]
    public async Task Retry_after_uncertain_delivery_checks_status_and_never_sends_a_second_copy()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.UncertainThenAccepted();
        var service = CreateService(db, gateway);
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var first = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);
        var second = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc.AddMinutes(1));

        Assert.Equal(SalesInvoiceStatus.DeliveryUncertain, first.Status);
        Assert.Equal(SalesInvoiceStatus.Issued, second.Status);
        Assert.Equal(1, gateway.SendCalls);
        Assert.Equal(1, gateway.StatusCalls);
        Assert.Single(await db.InvoiceVersions.Where(item => item.InvoiceId == draft.Id).ToListAsync());
    }

    [Fact]
    public async Task Transport_timeout_after_send_is_treated_as_uncertain_and_never_resent()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.TimedOutThenAccepted();
        var service = CreateService(db, gateway);
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var first = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);
        var second = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc.AddMinutes(1));

        Assert.Equal(SalesInvoiceStatus.DeliveryUncertain, first.Status);
        Assert.Equal(SalesInvoiceStatus.Issued, second.Status);
        Assert.Equal(1, gateway.SendCalls);
        Assert.Equal(1, gateway.StatusCalls);
    }

    [Fact]
    public async Task Fa3_rejection_is_saved_and_is_not_retried_as_a_new_send()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.Rejecting("FA(3): brak wymaganego pola");
        var service = CreateService(db, gateway);
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var result = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);

        Assert.Equal(SalesInvoiceStatus.Rejected, result.Status);
        Assert.Contains("brak", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, gateway.SendCalls);
        Assert.Equal(1, gateway.StatusCalls);
    }

    [Fact]
    public async Task Rejected_attempt_can_be_archived_corrected_and_sent_with_a_new_idempotency_key()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.RejectingThenAccepting("FA(3): brak wymaganego pola");
        var service = CreateService(db, gateway);
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var rejected = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);
        var rejectedIdempotencyKey = (await db.SalesInvoices.SingleAsync()).IdempotencyKey;

        await service.ReopenRejectedAsync("owner-1", draft.Id, NowUtc.AddMinutes(1));
        await service.EditDraftAsync("owner-1", draft.Id, 1_650m, 23m, NowUtc.AddMinutes(2));
        var accepted = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc.AddMinutes(3));

        var invoice = await db.SalesInvoices.SingleAsync();
        var versions = await db.InvoiceVersions
            .Where(item => item.InvoiceId == draft.Id)
            .OrderBy(item => item.VersionNumber)
            .ToListAsync();
        Assert.Equal(SalesInvoiceStatus.Rejected, rejected.Status);
        Assert.Equal(SalesInvoiceStatus.Issued, accepted.Status);
        Assert.NotEqual(rejectedIdempotencyKey, invoice.IdempotencyKey);
        Assert.Equal(2, gateway.SendCalls);
        Assert.Collection(
            versions,
            first =>
            {
                Assert.Equal(1, first.VersionNumber);
                Assert.Null(first.PreviousVersionId);
                Assert.Contains("Rejected", first.SnapshotJson, StringComparison.Ordinal);
            },
            second =>
            {
                Assert.Equal(2, second.VersionNumber);
                Assert.Equal(versions[0].Id, second.PreviousVersionId);
                Assert.Contains("Issued", second.SnapshotJson, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Automatic_issue_job_requests_a_retry_while_ksef_is_pending_and_does_not_resend()
    {
        var automaticNow = DateTimeOffset.Parse("2026-10-01T08:00:00Z");
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.PendingThenAccepted();
        var service = CreateService(db, gateway);
        await service.SetAutomationAsync("owner-1", enabled: true, warningAcknowledged: true, automaticNow);
        var payload = JsonSerializer.Serialize(new SalesMonthJobPayload(
            "owner-1",
            new DateOnly(2026, 10, 1)));
        var job = new LeasedBackgroundJob(
            Guid.NewGuid(),
            SalesInvoiceIssueJobHandler.JobTypeName,
            payload,
            "sales-issue:test",
            1,
            automaticNow.AddMinutes(1));
        var handler = new SalesInvoiceIssueJobHandler(service, new FixedTimeProvider(automaticNow));

        var retry = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(job));
        await service.SetAutomationAsync(
            "owner-1",
            enabled: false,
            warningAcknowledged: false,
            automaticNow.AddSeconds(30));
        var statusOnlyService = CreateService(
            db,
            gateway,
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = false });
        var statusOnlyHandler = new SalesInvoiceIssueJobHandler(
            statusOnlyService,
            new FixedTimeProvider(automaticNow));
        await statusOnlyHandler.ExecuteAsync(job with { AttemptNumber = 2 });

        var invoice = Assert.Single(await service.ListAsync("owner-1"));
        Assert.Contains("status", retry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SalesInvoiceStatus.Issued, invoice.Status);
        Assert.Equal(1, gateway.SendCalls);
        Assert.Equal(2, gateway.StatusCalls);
    }

    [Fact]
    public async Task Accepted_invoice_with_different_returned_content_is_flagged_and_never_resent()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.AcceptingWithDifferentContent();
        var service = CreateService(db, gateway);
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var result = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc);
        var repeated = await service.IssueAsync(
            "owner-1", draft.Id, automatic: false, new DateOnly(2026, 10, 2), NowUtc.AddMinutes(1));

        Assert.Equal(SalesInvoiceStatus.IssuedContentMismatch, result.Status);
        Assert.Equal(SalesInvoiceStatus.IssuedContentMismatch, repeated.Status);
        Assert.Contains("różni", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, gateway.SendCalls);
        Assert.Contains(await db.AuditEvents.ToListAsync(), item => item.Action == "sales.invoice.content-mismatch");
    }

    [Fact]
    public async Task Automation_is_off_by_default_and_requires_both_warning_and_operator_gate()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var gateway = FakeOutgoingKsefGateway.Accepting();
        var disabledService = CreateService(
            db,
            gateway,
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = false });
        var draft = await disabledService.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);

        var settings = await disabledService.GetAutomationAsync("owner-1");
        Assert.False(settings.Enabled);
        Assert.False(settings.CanBeEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabledService.SetAutomationAsync(
            "owner-1", enabled: true, warningAcknowledged: true, NowUtc));
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabledService.IssueAsync(
            "owner-1", draft.Id, automatic: true, new DateOnly(2026, 10, 1), NowUtc));

        var enabledService = CreateService(
            db,
            gateway,
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => enabledService.SetAutomationAsync(
            "owner-1", enabled: true, warningAcknowledged: false, NowUtc));
        await enabledService.SetAutomationAsync(
            "owner-1", enabled: true, warningAcknowledged: true, NowUtc);

        Assert.True((await enabledService.GetAutomationAsync("owner-1")).Enabled);
        Assert.Contains(await db.AuditEvents.ToListAsync(), item => item.Action == "sales.automation.enabled");
    }

    [Fact]
    public async Task New_future_rate_updates_plain_drafts_and_waits_for_confirmation_on_manual_drafts()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var sales = CreateService(db, new FakeOutgoingKsefGateway());
        var november = await sales.EnsureDraftAsync("owner-1", new DateOnly(2026, 11, 1), NowUtc);
        var december = await sales.EnsureDraftAsync("owner-1", new DateOnly(2026, 12, 1), NowUtc);
        await sales.EditDraftAsync("owner-1", december.Id, 1_650m, 23m, NowUtc.AddDays(1));

        await new CompanyProfileService(db).ChangeSubscriptionRateAsync(
            "owner-1",
            new DateOnly(2026, 11, 1),
            1_800m,
            23m,
            NowUtc.AddDays(2));

        var refreshedNovember = await sales.GetAsync("owner-1", november.Id);
        var refreshedDecember = await sales.GetAsync("owner-1", december.Id);
        Assert.Equal(1_800m, refreshedNovember!.Invoice.NetAmount);
        Assert.False(refreshedNovember.Invoice.HasPendingSubscriptionRate);
        Assert.Equal(1_650m, refreshedDecember!.Invoice.NetAmount);
        Assert.True(refreshedDecember.Invoice.HasPendingSubscriptionRate);
        Assert.Equal(1_800m, refreshedDecember.PendingSubscriptionNetAmount);
    }

    [Fact]
    public async Task Manual_and_automatic_paths_generate_the_same_business_document()
    {
        var manualGateway = FakeOutgoingKsefGateway.Accepting();
        var automaticGateway = FakeOutgoingKsefGateway.Accepting();
        var manualXml = await IssueInNewDatabaseAsync(manualGateway, automatic: false);
        var automaticXml = await IssueInNewDatabaseAsync(automaticGateway, automatic: true);

        Assert.Equal(RemoveTechnicalRowId(manualXml), RemoveTechnicalRowId(automaticXml));
    }

    [Fact]
    public async Task Two_parallel_workers_send_at_most_once()
    {
        var databaseName = $"firemka-sales-parallel-{Guid.NewGuid():N}";
        await using (var seed = CreateDbContext(databaseName))
        {
            await SeedCompanyAsync(seed);
            var service = CreateService(seed, new FakeOutgoingKsefGateway());
            await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);
        }

        var gateway = FakeOutgoingKsefGateway.BlockingPending();
        await using var firstDb = CreateDbContext(databaseName);
        await using var secondDb = CreateDbContext(databaseName);
        var invoiceId = await firstDb.SalesInvoices.Select(item => item.Id).SingleAsync();
        var first = CreateService(firstDb, gateway).IssueAsync(
            "owner-1", invoiceId, automatic: false, new DateOnly(2026, 10, 1), NowUtc);
        await gateway.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await CreateService(secondDb, gateway).IssueAsync(
            "owner-1", invoiceId, automatic: false, new DateOnly(2026, 10, 1), NowUtc);
        gateway.ReleaseSend.TrySetResult();
        await first;

        Assert.Equal(SalesInvoiceStatus.Sending, second.Status);
        Assert.Equal(1, gateway.SendCalls);
    }

    private static async Task<string> IssueInNewDatabaseAsync(
        FakeOutgoingKsefGateway gateway,
        bool automatic)
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var service = CreateService(
            db,
            gateway,
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = true });
        var draft = await service.EnsureDraftAsync("owner-1", new DateOnly(2026, 10, 1), NowUtc);
        if (automatic)
        {
            await service.SetAutomationAsync("owner-1", true, true, NowUtc);
        }

        await service.IssueAsync(
            "owner-1", draft.Id, automatic, new DateOnly(2026, 10, 1), NowUtc);
        return Assert.Single(gateway.SentXml);
    }

    private static string RemoveTechnicalRowId(string xml)
    {
        var document = XDocument.Parse(xml);
        XNamespace ns = Fa3InvoiceGenerator.Fa3Namespace;
        document.Descendants(ns + "UU_ID").Remove();
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static SalesInvoiceService CreateService(
        AppDbContext db,
        IOutgoingKsefGateway gateway,
        KsefOutgoingOptions? options = null)
        => new(
            db,
            gateway,
            new Fa3InvoiceGenerator(),
            new AuditTrail(db),
            options ?? new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = true });

    private static AppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"firemka-sales-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedCompanyAsync(AppDbContext db)
    {
        db.Companies.Add(Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowa 2, 00-002 Warszawa",
            new DateOnly(2026, 9, 15),
            "Testowy Klient",
            "1234563218",
            "Testowa 1, 00-001 Warszawa",
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            NowUtc));
        await db.SaveChangesAsync();
    }

    private sealed class FakeOutgoingKsefGateway : IOutgoingKsefGateway
    {
        private readonly bool _blockSend;
        private readonly bool _preserveReturnedContent;
        private readonly bool _throwUncertainOnFirstSend;
        private readonly bool _throwTaskCanceledOnFirstSend;
        private readonly IReadOnlyList<OutgoingKsefStatus> _statuses;

        private FakeOutgoingKsefGateway(
            OutgoingKsefStatus? status = null,
            IReadOnlyList<OutgoingKsefStatus>? statuses = null,
            bool throwUncertainOnFirstSend = false,
            bool throwTaskCanceledOnFirstSend = false,
            bool blockSend = false,
            bool preserveReturnedContent = false)
        {
            _statuses = statuses ?? [status ?? new OutgoingKsefStatus(OutgoingKsefProcessingState.Pending)];
            _throwUncertainOnFirstSend = throwUncertainOnFirstSend;
            _throwTaskCanceledOnFirstSend = throwTaskCanceledOnFirstSend;
            _blockSend = blockSend;
            _preserveReturnedContent = preserveReturnedContent;
        }

        public FakeOutgoingKsefGateway()
            : this(null)
        {
        }

        public int SendCalls { get; private set; }
        public int StatusCalls { get; private set; }
        public List<string> SentXml { get; } = [];
        public TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static FakeOutgoingKsefGateway Accepting() => new(new OutgoingKsefStatus(
            OutgoingKsefProcessingState.Accepted,
            "SESSION-1",
            "SUBMISSION-1",
            "1234563218-20261002-ABCDEF-ABCDEF-01",
            "<UPO>synthetic</UPO>",
            "MATCH-SENT-CONTENT"));

        public static FakeOutgoingKsefGateway AcceptingWithDifferentContent() => new(
            new OutgoingKsefStatus(
                OutgoingKsefProcessingState.Accepted,
                "SESSION-1",
                "SUBMISSION-1",
                "1234563218-20261002-ABCDEF-ABCDEF-01",
                "<UPO>synthetic</UPO>",
                "<Faktura>different-content</Faktura>"),
            preserveReturnedContent: true);

        public static FakeOutgoingKsefGateway Rejecting(string reason) => new(new OutgoingKsefStatus(
            OutgoingKsefProcessingState.Rejected,
            "SESSION-1",
            "SUBMISSION-1",
            RejectionReason: reason));

        public static FakeOutgoingKsefGateway RejectingThenAccepting(string reason) => new(
            statuses:
            [
                new OutgoingKsefStatus(
                    OutgoingKsefProcessingState.Rejected,
                    "SESSION-1",
                    "SUBMISSION-1",
                    RejectionReason: reason),
                Accepting()._statuses[0],
            ]);

        public static FakeOutgoingKsefGateway PendingThenAccepted() => new(
            statuses:
            [
                new OutgoingKsefStatus(OutgoingKsefProcessingState.Pending),
                Accepting()._statuses[0],
            ]);

        public static FakeOutgoingKsefGateway UncertainThenAccepted() => new(
            Accepting()._statuses[0],
            throwUncertainOnFirstSend: true);

        public static FakeOutgoingKsefGateway TimedOutThenAccepted() => new(
            Accepting()._statuses[0],
            throwTaskCanceledOnFirstSend: true);

        public static FakeOutgoingKsefGateway BlockingPending() => new(blockSend: true);

        public async Task<OutgoingKsefSubmission> SendAsync(
            OutgoingKsefInvoice invoice,
            CancellationToken cancellationToken = default)
        {
            SendCalls++;
            SentXml.Add(invoice.Fa3Xml);
            SendEntered.TrySetResult();
            if (_blockSend)
            {
                await ReleaseSend.Task.WaitAsync(cancellationToken);
            }

            if (_throwUncertainOnFirstSend && SendCalls == 1)
            {
                throw new OutgoingKsefDeliveryUncertainException("Połączenie przerwane po wysłaniu.");
            }

            if (_throwTaskCanceledOnFirstSend && SendCalls == 1)
            {
                throw new TaskCanceledException("Limit czasu minął po wysłaniu dokumentu.");
            }

            return new OutgoingKsefSubmission("SESSION-1", "SUBMISSION-1");
        }

        public Task<OutgoingKsefStatus> GetStatusAsync(
            string idempotencyKey,
            string? sessionReferenceNumber,
            string? submissionReferenceNumber,
            CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            var status = _statuses[Math.Min(StatusCalls - 1, _statuses.Count - 1)];
            if (status.State == OutgoingKsefProcessingState.Accepted
                && !_preserveReturnedContent
                && SentXml.Count > 0)
            {
                return Task.FromResult(status with { ReturnedInvoiceXml = SentXml[^1] });
            }

            return Task.FromResult(status);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
