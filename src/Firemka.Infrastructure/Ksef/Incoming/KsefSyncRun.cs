using Firemka.Application.ExternalServices;

namespace Firemka.Infrastructure.Ksef.Incoming;

public enum KsefSyncRunStatus
{
    Running,
    Completed,
    Failed,
}

public sealed class KsefSyncRun
{
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public KsefEnvironment Environment { get; set; }

    public KsefSyncRunStatus Status { get; set; }

    public string? StartedFromCursor { get; set; }

    public string? FinishedAtCursor { get; set; }

    public int ImportedCount { get; set; }

    public int UnchangedCount { get; set; }

    public int ConflictCount { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }
}

public sealed record KsefSyncResult(
    int ImportedCount,
    int UnchangedCount,
    int ConflictCount,
    string? Cursor);
