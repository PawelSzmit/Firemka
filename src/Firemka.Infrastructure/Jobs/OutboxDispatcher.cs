using Firemka.Application.Jobs;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Jobs;

public sealed class OutboxDispatcher(
    AppDbContext dbContext,
    IBackgroundJobQueue backgroundJobQueue)
{
    public async Task<bool> DispatchNextAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var message = await dbContext.OutboxMessages
            .Where(item => item.ProcessedAtUtc == null && item.AvailableAtUtc <= nowUtc)
            .OrderBy(item => item.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            return false;
        }

        await backgroundJobQueue.EnqueueAsync(
            new BackgroundJobCommand(
                message.MessageType,
                message.PayloadJson,
                message.IdempotencyKey,
                message.AvailableAtUtc),
            cancellationToken);
        message.ProcessedAtUtc = nowUtc;
        message.AttemptCount++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
