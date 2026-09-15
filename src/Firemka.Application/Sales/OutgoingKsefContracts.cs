namespace Firemka.Application.Sales;

public sealed record OutgoingKsefInvoice(
    string IdempotencyKey,
    string Fa3Xml);

public sealed record OutgoingKsefSubmission(
    string SessionReferenceNumber,
    string SubmissionReferenceNumber);

public enum OutgoingKsefProcessingState
{
    NotFound = 1,
    Pending = 2,
    Accepted = 3,
    Rejected = 4,
}

public sealed record OutgoingKsefStatus(
    OutgoingKsefProcessingState State,
    string? SessionReferenceNumber = null,
    string? SubmissionReferenceNumber = null,
    string? KsefNumber = null,
    string? UpoXml = null,
    string? ReturnedInvoiceXml = null,
    string? RejectionReason = null);

public interface IOutgoingKsefGateway
{
    Task<OutgoingKsefSubmission> SendAsync(
        OutgoingKsefInvoice invoice,
        CancellationToken cancellationToken = default);

    Task<OutgoingKsefStatus> GetStatusAsync(
        string idempotencyKey,
        string? sessionReferenceNumber,
        string? submissionReferenceNumber,
        CancellationToken cancellationToken = default);
}

public sealed class OutgoingKsefDeliveryUncertainException : Exception
{
    public OutgoingKsefDeliveryUncertainException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
