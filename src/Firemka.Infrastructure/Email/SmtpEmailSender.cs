using System.Net;
using System.Net.Mail;
using Firemka.Application.Notifications;

namespace Firemka.Infrastructure.Email;

public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!options.Enabled)
            throw new InvalidOperationException("Wysyłanie e-maili nie jest skonfigurowane.");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.From);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Recipient);
        if (options.Port is <= 0 or > 65_535) throw new InvalidOperationException("Port SMTP jest nieprawidłowy.");
        if (!options.EnableSsl)
            throw new InvalidOperationException("Wysyłanie SMTP bez szyfrowania TLS jest zablokowane.");

        using var mail = new MailMessage(options.From, message.Recipient)
        {
            Subject = message.Subject,
            Body = message.PlainTextBody,
            IsBodyHtml = false,
        };
        mail.Headers.Add("X-Firemka-Notification", Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{message.Recipient}|{message.Subject}|{message.PlainTextBody}")))
            .ToLowerInvariant());
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(options.Username)
                ? null
                : new NetworkCredential(options.Username, options.Password),
        };
        await client.SendMailAsync(mail, cancellationToken);
    }
}
