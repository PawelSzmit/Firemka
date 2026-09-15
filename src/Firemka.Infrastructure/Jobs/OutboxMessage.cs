namespace Firemka.Infrastructure.Jobs;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset AvailableAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
