using Firemka.Application.Jobs;
using Firemka.Application.Backups;
using Firemka.Application.MonthClosing;
using Firemka.Application.Notifications;
using Firemka.Application.Payments;
using Firemka.Domain.Companies;
using Firemka.Domain.Notifications;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Domain.Documents;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Companies;
using Firemka.Infrastructure.Email;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Notifications;
using Firemka.Infrastructure.Payments;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class PaymentNotificationWorkflowTests
{
    private const string Owner = "payment-owner";
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T08:00:00Z");

    [Fact]
    public async Task Full_payment_is_idempotent_and_a_corrected_month_has_a_new_unpaid_target()
    {
        await using var db = CreateDb();
        var company = await SeedClosedSeptemberAsync(db);
        var service = new PaymentService(db, new AuditTrail(db));
        var workspace = await service.GetAsync(Owner, company.Id, September);
        var vat = Assert.Single(workspace.Obligations, item => item.Kind == PaymentKind.Vat);
        Assert.Equal(new DateOnly(2026, 10, 26), vat.DueOn);

        var partial = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordFullAsync(
            Owner, company.Id, September,
            new(vat.Kind, vat.TargetId, vat.TargetVersion, vat.AmountDue - 0.01m,
                new DateOnly(2026, 10, 20), "manual:partial"), Now));
        Assert.Contains("pełną kwotę", partial.Message, StringComparison.OrdinalIgnoreCase);

        var command = new RecordFullPaymentCommand(
            vat.Kind, vat.TargetId, vat.TargetVersion, vat.AmountDue,
            new DateOnly(2026, 10, 20), "manual:transfer-1");
        var first = await service.RecordFullAsync(Owner, company.Id, September, command, Now);
        var replay = await service.RecordFullAsync(Owner, company.Id, September, command, Now.AddMinutes(1));
        Assert.Equal(first.Id, replay.Id);
        Assert.Single(await db.Payments.ToListAsync());

        var closing = new Firemka.Infrastructure.Calculations.MonthClosingService(db, new AuditTrail(db));
        await closing.StartCorrectionAsync(Owner, company.Id, September, "Zmiana danych", Now.AddMinutes(2));
        await closing.SaveDeclarationAsync(Owner, company.Id, September,
            new SaveMonthDeclarationCommand(0m, 1m, 0m, 0m, 0m, true, true, "synthetic:correction"),
            Now.AddMinutes(3));
        await closing.CloseAsync(Owner, company.Id, September, Now.AddMinutes(4));

        var corrected = await service.GetAsync(Owner, company.Id, September);
        var correctedVat = Assert.Single(corrected.Obligations, item => item.Kind == PaymentKind.Vat);
        Assert.Equal(2, correctedVat.TargetVersion);
        Assert.False(correctedVat.IsPaid);
        Assert.Single(corrected.PaymentHistory);
    }

    [Fact]
    public async Task Scheduler_deduplicates_deadlines_and_daily_errors_and_email_failure_is_retryable()
    {
        await using var db = CreateDb();
        _ = await SeedClosedSeptemberAsync(db);
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = "synthetic-failure",
            PayloadJson = "{}",
            IdempotencyKey = "synthetic-failure",
            State = BackgroundJobState.DeadLetter,
            MaximumAttempts = 1,
            AttemptCount = 1,
            AvailableAtUtc = Now,
            CreatedAtUtc = Now,
        });
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = NotificationEmailJobHandler.JobTypeName,
            PayloadJson = "{}",
            IdempotencyKey = "failed-notification-email",
            State = BackgroundJobState.DeadLetter,
            MaximumAttempts = 1,
            AttemptCount = 1,
            AvailableAtUtc = Now,
            CreatedAtUtc = Now,
        });
        await db.SaveChangesAsync();
        var options = new EmailOptions
        {
            Enabled = true,
            Recipient = "owner@example.test",
            BaseUrl = "https://firemka.example.test",
        };
        var scheduler = new NotificationScheduler(
            db, new PaymentService(db, new AuditTrail(db)), new TransactionalOutbox(db), options);

        var beforeEight = DateTimeOffset.Parse("2026-10-19T05:00:00Z");
        Assert.Equal(1, await scheduler.EnqueueDueAsync(beforeEight));
        Assert.Equal(0, await scheduler.EnqueueDueAsync(beforeEight.AddMinutes(1)));
        var afterEight = DateTimeOffset.Parse("2026-10-19T06:00:00Z");
        Assert.True(await scheduler.EnqueueDueAsync(afterEight) >= 1);
        Assert.Equal(0, await scheduler.EnqueueDueAsync(afterEight.AddMinutes(1)));
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.Kind == NotificationKind.Error
            || item.IssueKey.Contains("payment:Vat")));

        var nextDay = DateTimeOffset.Parse("2026-10-20T05:00:00Z");
        Assert.Equal(1, await scheduler.EnqueueDueAsync(nextDay));
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.Kind == NotificationKind.Error));

        var notification = await db.Notifications.SingleAsync(item => item.IssueKey.Contains("payment:Vat"));
        var sender = new FakeEmailSender { Failure = new InvalidOperationException("SMTP offline") };
        var clock = new FixedTimeProvider(afterEight);
        var handler = new NotificationEmailJobHandler(db, sender, options, clock);
        var payload = System.Text.Json.JsonSerializer.Serialize(
            new NotificationEmailJobPayload(notification.Id));
        var job = new LeasedBackgroundJob(Guid.NewGuid(), NotificationEmailJobHandler.JobTypeName,
            payload, "notification-test", 1, afterEight.AddMinutes(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(job));
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
        sender.Failure = null;
        await handler.ExecuteAsync(job);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(2, notification.AttemptCount);
        Assert.Single(sender.Messages);
        Assert.DoesNotContain("230", sender.Messages[0].PlainTextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Firma płatności", sender.Messages[0].PlainTextBody, StringComparison.Ordinal);
        Assert.Contains("https://firemka.example.test/", sender.Messages[0].PlainTextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scheduler_covers_7_2_0_invoice_overdue_and_a_reappearing_document_error()
    {
        await using var db = CreateDb();
        _ = await SeedClosedSeptemberAsync(db);
        var document = SourceDocument.CreateManual(Guid.NewGuid(), Owner, Now);
        document.MarkProcessingError(Now);
        db.SourceDocuments.Add(document);
        await db.SaveChangesAsync();
        var options = new EmailOptions
        {
            Enabled = true,
            Recipient = "owner@example.test",
            BaseUrl = "https://firemka.example.test",
        };
        var scheduler = new NotificationScheduler(
            db, new PaymentService(db, new AuditTrail(db)), new TransactionalOutbox(db), options);

        // Faktura wystawiona 3 września ma termin 10 września.
        Assert.Equal(2, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-09-11T06:00:00Z")));
        Assert.Single(await db.Notifications.Where(item =>
            item.IssueKey.Contains("payment:ClientInvoice")).ToListAsync());

        // ZUS ma termin 20, a VAT przesunięty termin 26 października: każdy 7, 2 i 0 dni wcześniej.
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-13T06:00:00Z")));
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-18T06:00:00Z")));
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-20T06:00:00Z")));
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-19T06:00:00Z")));
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-24T06:00:00Z")));
        Assert.Equal(3, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-26T07:00:00Z")));
        Assert.Equal(6, await db.Notifications.CountAsync(item =>
            item.IssueKey.Contains("payment:Vat") || item.IssueKey.Contains("payment:Zus")));

        // Rozwiązanie i ponowne pojawienie się błędu tego samego dnia to nowa sprawa.
        document.TransitionTo(SourceDocumentStatus.DataToReview, Now.AddMinutes(1));
        document.MarkProcessingError(Now.AddMinutes(2));
        await db.SaveChangesAsync();
        Assert.Equal(1, await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-09-11T06:10:00Z")));
        Assert.Equal(2, await db.Notifications.CountAsync(item =>
            item.IssueKey.Contains($"document-error:{document.Id:N}")
            && item.LocalDay == new DateOnly(2026, 9, 11)));
    }

    [Fact]
    public async Task Smtp_sender_rejects_unencrypted_configuration_before_network_access()
    {
        var sender = new SmtpEmailSender(new EmailOptions
        {
            Enabled = true,
            Host = "smtp.example.test",
            Port = 25,
            EnableSsl = false,
            From = "firemka@example.test",
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            new EmailMessage("owner@example.test", "Test", "Treść")));
        Assert.Contains("TLS", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_backup_warns_only_after_36_hours_and_dashboard_tracks_success_and_failure()
    {
        await using var db = CreateDb();
        var company = await SeedClosedSeptemberAsync(db);
        var backup = new BackupService(db);
        var issued = await backup.IssueTokenAsync(Owner, Now);
        var authorization = (await backup.AuthorizeAsync(issued.RawToken, Now))!;
        var options = new EmailOptions
        {
            Enabled = true,
            Recipient = "owner@example.test",
            BaseUrl = "https://firemka.example.test",
        };
        var scheduler = new NotificationScheduler(
            db, new PaymentService(db, new AuditTrail(db)), new TransactionalOutbox(db), options);

        await scheduler.EnqueueDueAsync(Now.AddHours(35));
        Assert.Empty(await db.Notifications.Where(item => item.Kind == NotificationKind.Backup).ToListAsync());
        await scheduler.EnqueueDueAsync(Now.AddHours(37));
        await scheduler.EnqueueDueAsync(Now.AddHours(37).AddMinutes(1));
        Assert.Single(await db.Notifications.Where(item => item.Kind == NotificationKind.Backup).ToListAsync());

        await backup.ReportAsync(authorization, new BackupReportCommand(
            true, "firemka-backup-20260915T000000000Z-v1.fmbak", new string('A', 64), 1, null, null, null),
            Now.AddHours(38));
        var successfulDashboard = await DashboardAsync(db, company.Id, Now.AddHours(39));
        Assert.Contains("Ostatnia poprawna kopia", successfulDashboard.BackupStatus, StringComparison.Ordinal);

        await backup.ReportAsync(authorization, new BackupReportCommand(
            false, null, null, null, "download-failed", null, null), Now.AddHours(40));
        var failedDashboard = await DashboardAsync(db, company.Id, Now.AddHours(46));
        Assert.Equal("Ostatnia próba kopii nie powiodła się", failedDashboard.BackupStatus);
        await scheduler.EnqueueDueAsync(Now.AddHours(46));
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.Kind == NotificationKind.Backup));
    }

    private static async Task<Company> SeedClosedSeptemberAsync(AppDbContext db)
    {
        var company = Company.Register(
            Owner, "Firma płatności", "1010000000", "Adres firmy", September,
            "Klient", "1234567890", "Adres klienta", "Usługa", 1_000m, 23m, 1m,
            VehicleArrangement.None, Now);
        db.Companies.Add(company);
        var invoice = SalesInvoice.CreateDraft(company.Id, Owner, September,
            company.GetSubscriptionRate(September).Id, company.ServiceDescription, 1_000m, 23m, Now);
        invoice.PrepareForIssue(September.AddDays(2), "FV/202609", false, "<Invoice />", Now);
        invoice.RegisterSubmission("session", "submission", Now);
        invoice.MarkIssued("1010000000-20260903-000000000000-00", "<UPO />", "<Invoice />", Now);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var closing = new Firemka.Infrastructure.Calculations.MonthClosingService(db, new AuditTrail(db));
        var first = await closing.GetAsync(Owner, company.Id, September, Now);
        await closing.ConfirmRuleSetAsync(Owner, first!.RuleSet!.Id,
            new ConfirmCalculationRuleSetCommand(true, "synthetic:rules", September), Now);
        await closing.SaveDeclarationAsync(Owner, company.Id, September,
            new SaveMonthDeclarationCommand(0m, 0m, 0m, 0m, 0m, true, true, "synthetic:month"), Now);
        await closing.CloseAsync(Owner, company.Id, September, Now);
        return company;
    }

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase10-{Guid.NewGuid():N}").Options);

    private static Task<Firemka.Application.Month.MonthDashboard> DashboardAsync(
        AppDbContext db,
        Guid companyId,
        DateTimeOffset nowUtc)
    {
        var closing = new Firemka.Infrastructure.Calculations.MonthClosingService(db, new AuditTrail(db));
        var payments = new PaymentService(db, new AuditTrail(db));
        var dashboard = new MonthDashboardQuery(db, closing, payments, new FixedTimeProvider(nowUtc));
        _ = companyId;
        return dashboard.GetAsync(Owner, September.Year, September.Month);
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public Exception? Failure { get; set; }
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
