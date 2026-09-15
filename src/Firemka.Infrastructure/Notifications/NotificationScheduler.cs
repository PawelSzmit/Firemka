using System.Text.Json;
using Firemka.Application.Jobs;
using Firemka.Application.Notifications;
using Firemka.Application.Payments;
using Firemka.Domain.Documents;
using Firemka.Domain.Backups;
using Firemka.Domain.Notifications;
using Firemka.Domain.Payments;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Email;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Notifications;

public sealed class NotificationScheduler(
    AppDbContext dbContext,
    IPaymentService paymentService,
    ITransactionalOutbox outbox,
    EmailOptions options) : INotificationScheduler
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public async Task<int> EnqueueDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return 0;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, Warsaw);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var afterDailyHour = localNow.Hour >= 8;
        var companies = await dbContext.Companies.AsNoTracking().ToListAsync(cancellationToken);
        var created = 0;

        foreach (var company in companies)
        {
            if (afterDailyHour)
            {
                var open = await paymentService.GetOpenAsync(
                    company.OwnerUserId, company.Id, cancellationToken);
                foreach (var obligation in open)
                {
                    var days = obligation.DueOn.DayNumber - today.DayNumber;
                    var shouldNotify = obligation.Kind == PaymentKind.ClientInvoice
                        ? days == -1
                        : days is 7 or 2 or 0;
                    if (!shouldNotify) continue;
                    created += await StageAsync(
                        company.OwnerUserId,
                        NotificationKind.Deadline,
                        $"payment:{obligation.Kind}:{obligation.TargetId:N}:v{obligation.TargetVersion}:d{days}",
                        today,
                        obligation.Kind == PaymentKind.ClientInvoice
                            ? "Firemka: faktura klienta po terminie"
                            : $"Firemka: zbliża się termin {obligation.Label}",
                        "Otwórz Firemkę i sprawdź należność oraz jej płatność.",
                        $"/Payments?year={obligation.Period.Year}&month={obligation.Period.Month}",
                        nowUtc,
                        cancellationToken);
                }

                var month = new DateOnly(today.Year, today.Month, 1);
                var currentInvoice = await dbContext.SalesInvoices.AsNoTracking().SingleOrDefaultAsync(
                    item => item.CompanyId == company.Id
                        && item.OwnerUserId == company.OwnerUserId
                        && item.ServiceMonth == month,
                    cancellationToken);
                if (currentInvoice is null || currentInvoice.Status is not (SalesInvoiceStatus.Issued or SalesInvoiceStatus.IssuedContentMismatch))
                {
                    created += await StageAsync(
                        company.OwnerUserId,
                        NotificationKind.InvoiceAction,
                        $"sales-invoice:{company.Id:N}:{month:yyyyMM}",
                        today,
                        "Firemka: faktura sprzedaży wymaga działania",
                        "Otwórz Firemkę i sprawdź fakturę sprzedaży za bieżący miesiąc.",
                        "/Invoices",
                        nowUtc,
                        cancellationToken);
                }
            }

            var documentErrors = await dbContext.SourceDocuments.AsNoTracking()
                .Where(item => item.OwnerUserId == company.OwnerUserId
                    && item.Status == SourceDocumentStatus.ErrorToResolve)
                .Select(item => new { item.Id, item.StateVersion })
                .ToListAsync(cancellationToken);
            foreach (var document in documentErrors)
            {
                created += await StageAsync(
                    company.OwnerUserId,
                    NotificationKind.Error,
                    $"document-error:{document.Id:N}:v{document.StateVersion}",
                    today,
                    "Firemka: nowy błąd wymaga działania",
                    "Otwórz Firemkę i sprawdź nierozwiązany błąd dokumentu.",
                    $"/Expenses/Review?id={document.Id}",
                    nowUtc,
                    cancellationToken);
            }

            // Starsze zadania nie przechowują właściciela. Przy jedynym koncie można je
            // bezpiecznie pokazać właścicielowi; przy wielu kontach pomijamy ogólny alert,
            // zamiast przypisać cudzą awarię do niewłaściwej osoby.
            if (companies.Count == 1)
            {
                var deadJobs = await dbContext.BackgroundJobs.AsNoTracking()
                    .Where(item => item.State == BackgroundJobState.DeadLetter
                        && item.JobType != NotificationEmailJobHandler.JobTypeName)
                    .Select(item => item.Id)
                    .ToListAsync(cancellationToken);
                foreach (var jobId in deadJobs)
                {
                    created += await StageAsync(
                        company.OwnerUserId,
                        NotificationKind.Error,
                        $"job-error:{jobId:N}",
                        today,
                        "Firemka: zadanie wymaga sprawdzenia",
                        "Otwórz Firemkę i sprawdź nierozwiązany błąd techniczny.",
                        "/Month",
                        nowUtc,
                        cancellationToken);
                }
            }

            if (afterDailyHour)
            {
                var hasBackupToken = await dbContext.BackupAccessTokens.AsNoTracking().AnyAsync(
                    item => item.OwnerUserId == company.OwnerUserId && item.RevokedAtUtc == null,
                    cancellationToken);
                var backupState = await dbContext.BackupStates.AsNoTracking().SingleOrDefaultAsync(
                    item => item.OwnerUserId == company.OwnerUserId,
                    cancellationToken);
                if (hasBackupToken && backupState is not null)
                {
                    var failed = backupState.LastFailureCode is not null
                        && (backupState.LastSuccessfulAtUtc is null
                            || backupState.LastAttemptAtUtc > backupState.LastSuccessfulAtUtc);
                    var overdue = backupState.IsOverdue(nowUtc, TimeSpan.FromHours(36), true);
                    if (failed || overdue)
                    {
                        var issueKey = failed
                            ? $"backup-failure:{backupState.LastAttemptAtUtc?.UtcTicks ?? 0}"
                            : "backup-overdue";
                        created += await StageAsync(
                            company.OwnerUserId,
                            NotificationKind.Backup,
                            issueKey,
                            today,
                            failed ? "Firemka: kopia nie powiodła się" : "Firemka: kopia jest zaległa",
                            "Otwórz Firemkę i sprawdź stan kopii zapasowej.",
                            "/Backups",
                            nowUtc,
                            cancellationToken);
                    }
                }
            }
        }

        return created;
    }

    private async Task<int> StageAsync(
        string owner,
        NotificationKind kind,
        string issueKey,
        DateOnly localDay,
        string subject,
        string body,
        string actionPath,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var deduplicationKey = $"{issueKey}:{localDay:yyyy-MM-dd}";
        if (await dbContext.Notifications.AsNoTracking().AnyAsync(
                item => item.OwnerUserId == owner && item.DeduplicationKey == deduplicationKey,
                cancellationToken))
            return 0;

        var notification = Notification.Create(
            owner, kind, issueKey, localDay, subject, body, actionPath, nowUtc);
        dbContext.Notifications.Add(notification);
        outbox.Stage(new OutboxCommand(
            NotificationEmailJobHandler.JobTypeName,
            JsonSerializer.Serialize(new NotificationEmailJobPayload(notification.Id)),
            $"notification-email:{notification.Id:N}",
            nowUtc));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return 1;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return 0;
        }
    }
}
