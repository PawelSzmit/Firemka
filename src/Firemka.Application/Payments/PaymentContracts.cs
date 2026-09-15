using Firemka.Domain.Payments;

namespace Firemka.Application.Payments;

public sealed record PaymentSnapshot(
    Guid Id,
    PaymentKind Kind,
    Guid TargetId,
    int TargetVersion,
    decimal AmountDue,
    decimal AmountPaid,
    DateOnly PaidOn,
    PaymentSource Source,
    string ExternalIdentifier,
    DateTimeOffset CreatedAtUtc);

public sealed record PaymentObligationSnapshot(
    PaymentKind Kind,
    Guid TargetId,
    int TargetVersion,
    DateOnly Period,
    string Label,
    decimal AmountDue,
    DateOnly DueOn,
    PaymentSnapshot? Payment)
{
    public bool IsPaid => Payment is not null;
}

public sealed record PaymentWorkspace(
    Guid CompanyId,
    string CompanyName,
    DateOnly Month,
    IReadOnlyList<PaymentObligationSnapshot> Obligations,
    IReadOnlyList<PaymentSnapshot> PaymentHistory);

public sealed record RecordFullPaymentCommand(
    PaymentKind Kind,
    Guid TargetId,
    int TargetVersion,
    decimal AmountPaid,
    DateOnly PaidOn,
    string ExternalIdentifier);

public interface IPaymentService
{
    Task<PaymentWorkspace> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentObligationSnapshot>> GetOpenAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<PaymentSnapshot> RecordFullAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        RecordFullPaymentCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
