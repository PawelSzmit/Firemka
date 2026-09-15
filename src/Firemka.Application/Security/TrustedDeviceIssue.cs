namespace Firemka.Application.Security;

public sealed record TrustedDeviceIssue(string Token, DateTimeOffset ExpiresAtUtc);
