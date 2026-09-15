namespace Firemka.Application.Security;

public sealed record AuthenticationAuditRecord(
    string? UserId,
    AuthenticationAuditOutcome Outcome,
    string Method,
    string? IpAddress);
