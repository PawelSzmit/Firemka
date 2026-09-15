using Firemka.Application.Jobs;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Jobs;

public sealed class BackgroundJobLeaseRenewer(
    DbContextOptions<AppDbContext> dbContextOptions) : IBackgroundJobLeaseRenewer
{
    public async Task<bool> RenewAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(dbContextOptions);
        return await new BackgroundJobQueue(dbContext).RenewLeaseAsync(
            jobId,
            workerId,
            nowUtc,
            leaseDuration,
            cancellationToken);
    }
}
