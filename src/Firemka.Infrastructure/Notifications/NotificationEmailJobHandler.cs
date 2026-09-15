using System.Text.Json;
using Firemka.Application.Jobs;
using Firemka.Application.Notifications;
using Firemka.Domain.Notifications;
using Firemka.Infrastructure.Email;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Notifications;

public sealed record NotificationEmailJobPayload(Guid NotificationId);

public sealed class NotificationEmailJobHandler(
    AppDbContext dbContext,
    IEmailSender sender,
    EmailOptions options,
    TimeProvider timeProvider) : IBackgroundJobHandler
{
    public const string JobTypeName = "notification-email-v1";
    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<NotificationEmailJobPayload>(job.PayloadJson)
            ?? throw new InvalidOperationException("Zadanie powiadomienia ma nieprawidłową treść.");
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == payload.NotificationId,
            cancellationToken) ?? throw new KeyNotFoundException("Nie znaleziono powiadomienia.");
        if (notification.Status == NotificationStatus.Sent) return;

        var actionUri = new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), notification.ActionPath.TrimStart('/'));
        var message = new EmailMessage(
            options.Recipient,
            notification.Subject,
            $"{notification.Body}\n\nOtwórz: {actionUri}\nDostęp wymaga zalogowania do Firemki.");
        try
        {
            await sender.SendAsync(message, cancellationToken);
            notification.MarkSent(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            notification.RecordFailure(exception.Message, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
