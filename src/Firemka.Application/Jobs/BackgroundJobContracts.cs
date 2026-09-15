namespace Firemka.Application.Jobs;

public sealed record BackgroundJobCommand(
    string JobType,
    string PayloadJson,
    string IdempotencyKey,
    DateTimeOffset AvailableAtUtc,
    int MaximumAttempts = 5);

public sealed record LeasedBackgroundJob(
    Guid Id,
    string JobType,
    string PayloadJson,
    string IdempotencyKey,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAtUtc);

public interface IBackgroundJobQueue
{
    Task<Guid> EnqueueAsync(
        BackgroundJobCommand command,
        CancellationToken cancellationToken = default);

    Task<LeasedBackgroundJob?> TryLeaseAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLeaseAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        Guid jobId,
        string workerId,
        string error,
        DateTimeOffset failedAtUtc,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}

public interface IBackgroundJobLeaseRenewer
{
    Task<bool> RenewAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}

public interface IBackgroundJobHandler
{
    string JobType { get; }

    Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default);
}

public sealed record OutboxCommand(
    string MessageType,
    string PayloadJson,
    string IdempotencyKey,
    DateTimeOffset OccurredAtUtc);

public interface ITransactionalOutbox
{
    Guid Stage(OutboxCommand command);
}
