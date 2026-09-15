namespace Firemka.Application.Notifications;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string PlainTextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public interface INotificationScheduler
{
    Task<int> EnqueueDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
