using Firemka.Application.ExternalServices;

namespace Firemka.Infrastructure.Ksef.Incoming;

public sealed class KsefSyncCheckpoint
{
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public KsefEnvironment Environment { get; set; }

    public DateTimeOffset InitialFromUtc { get; set; }

    public string? Cursor { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
