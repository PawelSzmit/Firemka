using Firemka.Application.Jobs;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Jobs;

public sealed class BackgroundJobQueue(AppDbContext dbContext) : IBackgroundJobQueue
{
    public async Task<Guid> EnqueueAsync(
        BackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.MaximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Liczba prób musi być dodatnia.");
        }
        var tracked = dbContext.BackgroundJobs.Local.FirstOrDefault(
            job => job.IdempotencyKey == command.IdempotencyKey);
        if (tracked is not null)
        {
            return tracked.Id;
        }

        if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            var jobId = Guid.NewGuid();
            var createdAtUtc = DateTimeOffset.UtcNow;
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "BackgroundJobs"
                    ("Id", "JobType", "PayloadJson", "IdempotencyKey", "State",
                     "AttemptCount", "MaximumAttempts", "AvailableAtUtc", "CreatedAtUtc")
                VALUES
                    ({jobId}, {command.JobType}, CAST({command.PayloadJson} AS jsonb),
                     {command.IdempotencyKey}, {BackgroundJobState.Pending.ToString()},
                     {0}, {command.MaximumAttempts}, {command.AvailableAtUtc}, {createdAtUtc})
                ON CONFLICT ("IdempotencyKey") DO NOTHING
                """,
                cancellationToken);

            return await dbContext.BackgroundJobs
                .AsNoTracking()
                .Where(job => job.IdempotencyKey == command.IdempotencyKey)
                .Select(job => job.Id)
                .SingleAsync(cancellationToken);
        }

        var existingId = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.IdempotencyKey == command.IdempotencyKey)
            .Select(job => (Guid?)job.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            return existingId.Value;
        }

        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = command.JobType,
            PayloadJson = command.PayloadJson,
            IdempotencyKey = command.IdempotencyKey,
            State = BackgroundJobState.Pending,
            MaximumAttempts = command.MaximumAttempts,
            AvailableAtUtc = command.AvailableAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task<LeasedBackgroundJob?> TryLeaseAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        for (var retry = 0; retry < 5; retry++)
        {
            var candidateId = await EligibleJobs(nowUtc)
                .OrderBy(job => job.AvailableAtUtc)
                .ThenBy(job => job.CreatedAtUtc)
                .Select(job => (Guid?)job.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (candidateId is null)
            {
                return null;
            }

            if (dbContext.Database.IsRelational())
            {
                var updated = await dbContext.BackgroundJobs
                    .Where(job => job.Id == candidateId.Value
                        && ((job.State == BackgroundJobState.Pending && job.AvailableAtUtc <= nowUtc)
                            || (job.State == BackgroundJobState.Running && job.LeaseExpiresAtUtc <= nowUtc)))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(job => job.State, BackgroundJobState.Running)
                            .SetProperty(job => job.LeaseOwner, workerId)
                            .SetProperty(job => job.LeaseExpiresAtUtc, nowUtc.Add(leaseDuration))
                            .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1),
                        cancellationToken);
                if (updated == 0)
                {
                    continue;
                }

                var leased = await dbContext.BackgroundJobs.AsNoTracking()
                    .SingleAsync(job => job.Id == candidateId.Value, cancellationToken);
                return ToLease(leased);
            }

            var inMemoryJob = await dbContext.BackgroundJobs.SingleAsync(
                job => job.Id == candidateId.Value,
                cancellationToken);
            inMemoryJob.State = BackgroundJobState.Running;
            inMemoryJob.LeaseOwner = workerId;
            inMemoryJob.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            inMemoryJob.AttemptCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToLease(inMemoryJob);
        }

        return null;
    }

    public async Task CompleteAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var job = await GetOwnedRunningJobAsync(jobId, workerId, cancellationToken);
        job.State = BackgroundJobState.Completed;
        job.CompletedAtUtc = completedAtUtc;
        job.LeaseOwner = null;
        job.LeaseExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RenewLeaseAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        var renewedUntilUtc = nowUtc.Add(leaseDuration);
        if (dbContext.Database.IsRelational())
        {
            var updated = await dbContext.BackgroundJobs
                .Where(job => job.Id == jobId
                    && job.State == BackgroundJobState.Running
                    && job.LeaseOwner == workerId
                    && job.LeaseExpiresAtUtc > nowUtc)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        job => job.LeaseExpiresAtUtc,
                        renewedUntilUtc),
                    cancellationToken);
            return updated == 1;
        }

        var inMemoryJob = await dbContext.BackgroundJobs.SingleOrDefaultAsync(
            job => job.Id == jobId
                && job.State == BackgroundJobState.Running
                && job.LeaseOwner == workerId
                && job.LeaseExpiresAtUtc > nowUtc,
            cancellationToken);
        if (inMemoryJob is null)
        {
            return false;
        }

        inMemoryJob.LeaseExpiresAtUtc = renewedUntilUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task FailAsync(
        Guid jobId,
        string workerId,
        string error,
        DateTimeOffset failedAtUtc,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var job = await GetOwnedRunningJobAsync(jobId, workerId, cancellationToken);
        job.LastError = error.Length <= 2_000 ? error : error[..2_000];
        job.LeaseOwner = null;
        job.LeaseExpiresAtUtc = null;
        if (job.AttemptCount >= job.MaximumAttempts)
        {
            job.State = BackgroundJobState.DeadLetter;
        }
        else
        {
            job.State = BackgroundJobState.Pending;
            job.AvailableAtUtc = failedAtUtc.Add(retryDelay);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<BackgroundJob> EligibleJobs(DateTimeOffset nowUtc)
    {
        return dbContext.BackgroundJobs.Where(job =>
            (job.State == BackgroundJobState.Pending && job.AvailableAtUtc <= nowUtc)
            || (job.State == BackgroundJobState.Running && job.LeaseExpiresAtUtc <= nowUtc));
    }

    private async Task<BackgroundJob> GetOwnedRunningJobAsync(
        Guid jobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs.SingleOrDefaultAsync(
            item => item.Id == jobId
                && item.State == BackgroundJobState.Running
                && item.LeaseOwner == workerId,
            cancellationToken);
        return job ?? throw new InvalidOperationException("Zadanie nie ma aktywnej dzierżawy tego workera.");
    }

    private static LeasedBackgroundJob ToLease(BackgroundJob job)
    {
        return new LeasedBackgroundJob(
            job.Id,
            job.JobType,
            job.PayloadJson,
            job.IdempotencyKey,
            job.AttemptCount,
            job.LeaseExpiresAtUtc!.Value);
    }
}
