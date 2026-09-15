using Firemka.Application.Jobs;
using Firemka.Infrastructure.Persistence;

namespace Firemka.Infrastructure.Jobs;

public sealed class TransactionalOutbox(AppDbContext dbContext) : ITransactionalOutbox
{
    public Guid Stage(OutboxCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var existing = dbContext.OutboxMessages.Local.FirstOrDefault(
            message => message.IdempotencyKey == command.IdempotencyKey);
        if (existing is not null)
        {
            return existing.Id;
        }

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = command.MessageType,
            PayloadJson = command.PayloadJson,
            IdempotencyKey = command.IdempotencyKey,
            OccurredAtUtc = command.OccurredAtUtc,
            AvailableAtUtc = command.OccurredAtUtc,
        };
        dbContext.OutboxMessages.Add(message);
        return message.Id;
    }
}
