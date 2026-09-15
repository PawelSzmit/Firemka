using Firemka.Application.Auditing;
using Firemka.Infrastructure.Persistence;

namespace Firemka.Infrastructure.Auditing;

public sealed class AuditTrail(AppDbContext dbContext) : IAuditTrail
{
    public Guid Stage(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var id = Guid.NewGuid();
        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = id,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Actor = record.Actor,
            OccurredAtUtc = record.OccurredAtUtc,
            DetailsJson = record.DetailsJson,
        });
        return id;
    }
}
