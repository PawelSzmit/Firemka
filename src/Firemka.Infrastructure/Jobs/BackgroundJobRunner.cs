using Firemka.Application.Jobs;

namespace Firemka.Infrastructure.Jobs;

public sealed class BackgroundJobRunner
{
    private readonly IReadOnlyDictionary<string, IBackgroundJobHandler> _handlers;
    private readonly IBackgroundJobLeaseRenewer _leaseRenewer;
    private readonly BackgroundJobRunnerOptions _options;
    private readonly IBackgroundJobQueue _queue;

    public BackgroundJobRunner(
        IBackgroundJobQueue queue,
        IEnumerable<IBackgroundJobHandler> handlers,
        IBackgroundJobLeaseRenewer leaseRenewer,
        BackgroundJobRunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(leaseRenewer);
        ArgumentNullException.ThrowIfNull(options);
        if (options.LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Czas leasingu musi być dodatni.");
        }

        if (options.HeartbeatInterval <= TimeSpan.Zero
            || options.HeartbeatInterval >= options.LeaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Odstęp odnawiania musi być dodatni i krótszy od czasu leasingu.");
        }

        _queue = queue;
        _handlers = handlers.ToDictionary(handler => handler.JobType, StringComparer.Ordinal);
        _leaseRenewer = leaseRenewer;
        _options = options;
    }

    public async Task<bool> RunNextAsync(
        string workerId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var job = await _queue.TryLeaseAsync(
            workerId,
            nowUtc,
            _options.LeaseDuration,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            if (!_handlers.TryGetValue(job.JobType, out var handler))
            {
                throw new InvalidOperationException($"Brak wykonawcy zadania typu {job.JobType}.");
            }

            await ExecuteWithLeaseHeartbeatAsync(
                handler,
                job,
                workerId,
                cancellationToken);
            await _queue.CompleteAsync(job.Id, workerId, nowUtc, cancellationToken);
        }
        catch (BackgroundJobLeaseLostException)
        {
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var delayMinutes = Math.Min(30, Math.Pow(2, Math.Max(0, job.AttemptNumber - 1)));
            await _queue.FailAsync(
                job.Id,
                workerId,
                exception.Message,
                nowUtc,
                TimeSpan.FromMinutes(delayMinutes),
                cancellationToken);
        }

        return true;
    }

    private async Task ExecuteWithLeaseHeartbeatAsync(
        IBackgroundJobHandler handler,
        LeasedBackgroundJob job,
        string workerId,
        CancellationToken cancellationToken)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var execution = handler.ExecuteAsync(job, executionCancellation.Token);
        var heartbeat = MaintainLeaseAsync(
            job.Id,
            workerId,
            heartbeatCancellation.Token);

        try
        {
            var firstCompleted = await Task.WhenAny(execution, heartbeat);
            if (firstCompleted == heartbeat)
            {
                bool leaseMaintained;
                try
                {
                    leaseMaintained = await heartbeat;
                }
                catch
                {
                    executionCancellation.Cancel();
                    await ObserveCancellationAsync(execution, executionCancellation.Token);
                    throw;
                }

                if (!leaseMaintained)
                {
                    executionCancellation.Cancel();
                    await ObserveCancellationAsync(execution, executionCancellation.Token);
                    throw new BackgroundJobLeaseLostException();
                }
            }

            await execution;
            if (heartbeat.IsCompleted && !await heartbeat)
            {
                throw new BackgroundJobLeaseLostException();
            }
        }
        finally
        {
            heartbeatCancellation.Cancel();
            await heartbeat;
        }
    }

    private static async Task ObserveCancellationAsync(
        Task execution,
        CancellationToken cancellationToken)
    {
        try
        {
            await execution;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> MaintainLeaseAsync(
        Guid jobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_options.HeartbeatInterval, cancellationToken);
                if (!await _leaseRenewer.RenewAsync(
                        jobId,
                        workerId,
                        DateTimeOffset.UtcNow,
                        _options.LeaseDuration,
                        cancellationToken))
                {
                    return false;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private sealed class BackgroundJobLeaseLostException : Exception;
}
