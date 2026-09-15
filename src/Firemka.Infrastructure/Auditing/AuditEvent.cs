namespace Firemka.Infrastructure.Auditing;

public sealed class AuditEvent
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string DetailsJson { get; set; } = "{}";
}
