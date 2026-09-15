using Firemka.Application.Auditing;
using Firemka.Application.Jobs;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class DurableJobTests
{
    [Fact]
    public async Task Two_identical_commands_create_one_background_job()
    {
        await using var dbContext = CreateDbContext();
        var queue = new BackgroundJobQueue(dbContext);
        var command = new BackgroundJobCommand(
            "document.extract",
            "{\"storedFileId\":\"11111111-1111-1111-1111-111111111111\"}",
            "document.extract:11111111-1111-1111-1111-111111111111",
            DateTimeOffset.Parse("2026-09-10T08:00:00Z"));

        var firstId = await queue.EnqueueAsync(command);
        var secondId = await queue.EnqueueAsync(command);

        Assert.Equal(firstId, secondId);
        Assert.Single(await dbContext.BackgroundJobs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Expired_lease_returns_the_same_job_to_another_worker()
    {
        await using var dbContext = CreateDbContext();
        var queue = new BackgroundJobQueue(dbContext);
        var availableAt = DateTimeOffset.Parse("2026-09-10T08:00:00Z");
        var jobId = await queue.EnqueueAsync(
            new BackgroundJobCommand("test.crash", "{}", "test.crash:1", availableAt));

        var firstLease = await queue.TryLeaseAsync(
            "worker-a",
            availableAt,
            TimeSpan.FromMinutes(1));
        var leaseWhileRunning = await queue.TryLeaseAsync(
            "worker-b",
            availableAt.AddSeconds(30),
            TimeSpan.FromMinutes(1));
        var recoveredLease = await queue.TryLeaseAsync(
            "worker-b",
            availableAt.AddSeconds(61),
            TimeSpan.FromMinutes(1));

        Assert.NotNull(firstLease);
        Assert.Equal(jobId, firstLease.Id);
        Assert.Equal(1, firstLease.AttemptNumber);
        Assert.Null(leaseWhileRunning);
        Assert.NotNull(recoveredLease);
        Assert.Equal(jobId, recoveredLease.Id);
        Assert.Equal(2, recoveredLease.AttemptNumber);
    }

    [Fact]
    public async Task Failed_job_waits_for_its_retry_delay()
    {
        await using var dbContext = CreateDbContext();
        var queue = new BackgroundJobQueue(dbContext);
        var now = DateTimeOffset.Parse("2026-09-10T08:00:00Z");
        var jobId = await queue.EnqueueAsync(
            new BackgroundJobCommand("test.retry", "{}", "test.retry:1", now));
        var lease = await queue.TryLeaseAsync("worker-a", now, TimeSpan.FromMinutes(1));
        Assert.NotNull(lease);

        await queue.FailAsync(
            jobId,
            "worker-a",
            "synthetic failure",
            now,
            TimeSpan.FromMinutes(5));

        Assert.Null(await queue.TryLeaseAsync("worker-b", now.AddMinutes(4), TimeSpan.FromMinutes(1)));
        var retry = await queue.TryLeaseAsync("worker-b", now.AddMinutes(5), TimeSpan.FromMinutes(1));
        Assert.NotNull(retry);
        Assert.Equal(2, retry.AttemptNumber);
    }

    [Fact]
    public async Task Outbox_and_audit_entries_are_staged_until_the_transaction_is_saved()
    {
        await using var dbContext = CreateDbContext();
        var outbox = new TransactionalOutbox(dbContext);
        var audit = new AuditTrail(dbContext);
        var occurredAt = DateTimeOffset.Parse("2026-09-10T08:00:00Z");

        var firstOutboxId = outbox.Stage(
            new OutboxCommand("document.acquired", "{}", "document.acquired:1", occurredAt));
        var duplicateOutboxId = outbox.Stage(
            new OutboxCommand("document.acquired", "{}", "document.acquired:1", occurredAt));
        audit.Stage(new AuditRecord(
            "document.acquired",
            "SourceDocument",
            "11111111-1111-1111-1111-111111111111",
            "owner",
            occurredAt,
            "{}"));

        Assert.Equal(firstOutboxId, duplicateOutboxId);
        Assert.Empty(await dbContext.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.AuditEvents.AsNoTracking().ToListAsync());

        await dbContext.SaveChangesAsync();

        Assert.Single(await dbContext.OutboxMessages.AsNoTracking().ToListAsync());
        Assert.Single(await dbContext.AuditEvents.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Replayed_outbox_dispatch_creates_one_job_and_marks_the_message_processed()
    {
        await using var dbContext = CreateDbContext();
        var queue = new BackgroundJobQueue(dbContext);
        var outbox = new TransactionalOutbox(dbContext);
        var occurredAt = DateTimeOffset.Parse("2026-09-10T08:00:00Z");
        outbox.Stage(new OutboxCommand(
            "document.extract",
            "{}",
            "document.extract:replayed",
            occurredAt));
        await dbContext.SaveChangesAsync();

        await queue.EnqueueAsync(new BackgroundJobCommand(
            "document.extract",
            "{}",
            "document.extract:replayed",
            occurredAt));

        var dispatcher = new OutboxDispatcher(dbContext, queue);
        Assert.True(await dispatcher.DispatchNextAsync(occurredAt));

        Assert.Single(await dbContext.BackgroundJobs.AsNoTracking().ToListAsync());
        var message = await dbContext.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(occurredAt, message.ProcessedAtUtc);
    }

    [Fact]
    public async Task Runner_completes_a_job_after_its_registered_handler_succeeds()
    {
        var options = CreateDbContextOptions();
        await using var dbContext = new AppDbContext(options);
        var queue = new BackgroundJobQueue(dbContext);
        var now = DateTimeOffset.Parse("2026-09-10T08:00:00Z");
        await queue.EnqueueAsync(new BackgroundJobCommand(
            "test.success",
            "{}",
            "test.success:1",
            now));
        var handler = new SuccessfulJobHandler();
        var runner = new BackgroundJobRunner(
            queue,
            [handler],
            new BackgroundJobLeaseRenewer(options),
            new BackgroundJobRunnerOptions());

        Assert.True(await runner.RunNextAsync("worker-a", now));

        Assert.Equal(1, handler.ExecutionCount);
        var job = await dbContext.BackgroundJobs.AsNoTracking().SingleAsync();
        Assert.Equal(BackgroundJobState.Completed, job.State);
        Assert.Equal(now, job.CompletedAtUtc);
    }

    [Fact]
    public async Task Long_running_job_renews_its_lease_before_another_worker_can_take_it()
    {
        var options = CreateDbContextOptions();
        await using var dbContext = new AppDbContext(options);
        var queue = new BackgroundJobQueue(dbContext);
        var availableAt = DateTimeOffset.UtcNow;
        await queue.EnqueueAsync(new BackgroundJobCommand(
            "test.long-running",
            "{}",
            "test.long-running:1",
            availableAt));
        var handler = new BlockingJobHandler();
        // Keep a realistic scheduling margin: the full solution runs many test assemblies
        // in parallel, so a 120 ms lease could expire before the first timer continuation.
        var leaseDuration = TimeSpan.FromSeconds(5);
        var renewingObserver = new RenewingObserver(new BackgroundJobLeaseRenewer(options));
        var runner = new BackgroundJobRunner(
            queue,
            [handler],
            renewingObserver,
            new BackgroundJobRunnerOptions
            {
                LeaseDuration = leaseDuration,
                HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            });

        var leaseStartedAt = DateTimeOffset.UtcNow;
        var execution = runner.RunNextAsync("worker-a", leaseStartedAt);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await renewingObserver.TwoRenewalsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var contenderContext = new AppDbContext(options);
        var contender = new BackgroundJobQueue(contenderContext);
        Assert.Null(await contender.TryLeaseAsync(
            "worker-b",
            leaseStartedAt.Add(leaseDuration).AddMilliseconds(1),
            leaseDuration));

        handler.Release.TrySetResult();
        Assert.True(await execution.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Lost_lease_cancels_the_handler_without_overwriting_the_new_owner()
    {
        var options = CreateDbContextOptions();
        await using var dbContext = new AppDbContext(options);
        var queue = new BackgroundJobQueue(dbContext);
        var now = DateTimeOffset.UtcNow;
        await queue.EnqueueAsync(new BackgroundJobCommand(
            "test.lease-loss",
            "{}",
            "test.lease-loss:1",
            now));
        var handler = new CancellationAwareJobHandler();
        var runner = new BackgroundJobRunner(
            queue,
            [handler],
            new LeaseStealingRenewer(options),
            new BackgroundJobRunnerOptions
            {
                LeaseDuration = TimeSpan.FromMilliseconds(120),
                HeartbeatInterval = TimeSpan.FromMilliseconds(20),
            });

        Assert.True(await runner.RunNextAsync("worker-a", now).WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.True(handler.WasCancelled);
        var job = await dbContext.BackgroundJobs.AsNoTracking().SingleAsync();
        Assert.Equal(BackgroundJobState.Running, job.State);
        Assert.Equal("worker-b", job.LeaseOwner);
        Assert.Null(job.CompletedAtUtc);
    }

    private static AppDbContext CreateDbContext()
    {
        return new AppDbContext(CreateDbContextOptions());
    }

    private static DbContextOptions<AppDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"firemka-jobs-{Guid.NewGuid():N}")
            .Options;
    }

    private sealed class SuccessfulJobHandler : IBackgroundJobHandler
    {
        public string JobType => "test.success";

        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(
            LeasedBackgroundJob job,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingJobHandler : IBackgroundJobHandler
    {
        public string JobType => "test.long-running";

        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(
            LeasedBackgroundJob job,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CancellationAwareJobHandler : IBackgroundJobHandler
    {
        public string JobType => "test.lease-loss";

        public bool WasCancelled { get; private set; }

        public async Task ExecuteAsync(
            LeasedBackgroundJob job,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                WasCancelled = true;
                throw;
            }
        }
    }

    private sealed class LeaseStealingRenewer(
        DbContextOptions<AppDbContext> options) : IBackgroundJobLeaseRenewer
    {
        public async Task<bool> RenewAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            await using var dbContext = new AppDbContext(options);
            var job = await dbContext.BackgroundJobs.SingleAsync(
                item => item.Id == jobId,
                cancellationToken);
            job.LeaseOwner = "worker-b";
            job.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private sealed class RenewingObserver(
        IBackgroundJobLeaseRenewer inner) : IBackgroundJobLeaseRenewer
    {
        private int _successfulRenewals;

        public TaskCompletionSource TwoRenewalsCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> RenewAsync(
            Guid jobId,
            string workerId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var renewed = await inner.RenewAsync(
                jobId,
                workerId,
                nowUtc,
                leaseDuration,
                cancellationToken);
            if (renewed && Interlocked.Increment(ref _successfulRenewals) >= 2)
            {
                TwoRenewalsCompleted.TrySetResult();
            }

            return renewed;
        }
    }
}
