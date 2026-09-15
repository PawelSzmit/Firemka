using Firemka.Application.Sales;

namespace Firemka.Infrastructure.Ksef.Outgoing;

public sealed class UnconfiguredOutgoingKsefGateway(KsefOutgoingOptions options) : IOutgoingKsefGateway
{
    public Task<OutgoingKsefSubmission> SendAsync(
        OutgoingKsefInvoice invoice,
        CancellationToken cancellationToken = default)
    {
        options.ValidateForUse();
        throw NotConfigured();
    }

    public Task<OutgoingKsefStatus> GetStatusAsync(
        string idempotencyKey,
        string? sessionReferenceNumber,
        string? submissionReferenceNumber,
        CancellationToken cancellationToken = default)
    {
        options.ValidateForUse();
        throw NotConfigured();
    }

    private static InvalidOperationException NotConfigured() => new(
        "Adapter wysyłki KSeF pozostaje zablokowany do odbioru na środowisku testowym i zatwierdzenia klienta KSeF.");
}
