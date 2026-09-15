namespace Firemka.Infrastructure.Identity;

public sealed record OwnerCreationResult(
    OwnerCreationStatus Status,
    string? UserId,
    IReadOnlyList<string> Errors);
