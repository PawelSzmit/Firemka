namespace Firemka.Infrastructure.Identity;

public sealed class OwnerBootstrap
{
    public const int SingletonId = 1;

    public int Id { get; set; }

    public string? OwnerUserId { get; set; }

    public DateTimeOffset? SetupStartedAtUtc { get; set; }

    public DateTimeOffset? SetupCompletedAtUtc { get; set; }

    public bool IsCompleted => OwnerUserId is not null && SetupCompletedAtUtc is not null;
}
