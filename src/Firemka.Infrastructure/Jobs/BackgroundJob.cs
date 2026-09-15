namespace Firemka.Infrastructure.Jobs;

public enum BackgroundJobState
{
    Pending,
    Running,
    Completed,
    DeadLetter,
}

public sealed class BackgroundJob
{
    public Guid Id { get; set; }

    public string JobType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string IdempotencyKey { get; set; } = string.Empty;

    public BackgroundJobState State { get; set; }

    public int AttemptCount { get; set; }

    public int MaximumAttempts { get; set; } = 5;

    public DateTimeOffset AvailableAtUtc { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
