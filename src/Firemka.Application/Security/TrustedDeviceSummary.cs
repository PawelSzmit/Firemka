namespace Firemka.Application.Security;

public sealed record TrustedDeviceSummary(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUsedAtUtc,
    DateTimeOffset ExpiresAtUtc);
