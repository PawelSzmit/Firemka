namespace Firemka.Infrastructure.Security;

public sealed class LoginAuditEvent
{
    public Guid Id { get; set; }

    public string? UserId { get; set; }

    public required string Outcome { get; set; }

    public required string Method { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
