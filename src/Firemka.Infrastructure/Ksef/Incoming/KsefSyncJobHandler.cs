using System.Text.Json;
using Firemka.Application.Jobs;

namespace Firemka.Infrastructure.Ksef.Incoming;

public sealed class KsefSyncJobHandler(KsefIncomingSyncService syncService) : IBackgroundJobHandler
{
    public const string JobTypeName = "ksef.incoming.sync";

    public string JobType => JobTypeName;

    public Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<KsefSyncJobPayload>(job.PayloadJson)
            ?? throw new InvalidDataException("Zadanie synchronizacji KSeF ma niepoprawne dane.");
        return syncService.SyncAsync(payload.OwnerUserId, payload.InitialFromUtc, cancellationToken);
    }
}

public sealed record KsefSyncJobPayload(string OwnerUserId, DateTimeOffset InitialFromUtc);
