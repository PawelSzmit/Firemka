namespace Firemka.Infrastructure.Jobs;

public sealed class BackgroundJobRunnerOptions
{
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
}
