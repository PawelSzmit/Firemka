namespace Firemka.Application.Auditing;

public sealed record AuditRecord(
    string Action,
    string EntityType,
    string EntityId,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string DetailsJson);

public interface IAuditTrail
{
    Guid Stage(AuditRecord record);
}
