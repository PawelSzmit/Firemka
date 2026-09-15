using Firemka.Domain.Accounting;

namespace Firemka.Application.Accounting;

public sealed record PrepareCostCommand(
    string SellerCountryCode,
    VatTreatment VatTreatment,
    decimal? VatRate,
    CostServiceKind ServiceKind,
    decimal GrossAmount,
    decimal InputVatAmount,
    bool SellerNameFallbackConfirmed = false);

public sealed record CostDecisionCommand(
    string KpirCategory,
    decimal VatDeductionPercent,
    decimal KpirCostPercent,
    AccountingPeriodPolicy KpirPeriodPolicy,
    AccountingPeriodPolicy VatPeriodPolicy,
    DateOnly KpirPeriod,
    DateOnly VatPeriod,
    string DecisionSource);

public sealed record CostBookingSnapshot(
    Guid Id,
    Guid SourceDocumentId,
    Guid CompanyId,
    CostBookingStatus Status,
    string SellerKey,
    string SellerCountryCode,
    string Currency,
    VatTreatment VatTreatment,
    decimal? VatRate,
    CostServiceKind ServiceKind,
    decimal GrossAmount,
    decimal InputVatAmount,
    DateOnly IssueDate,
    Guid? RuleId,
    int? RuleVersion,
    decimal? DeductibleVatAmount,
    decimal? KpirAmount,
    bool Automatic,
    string? Explanation);

public sealed record CostReviewSnapshot(
    CostBookingSnapshot Booking,
    IReadOnlyList<string> Differences);

public sealed record KpirEntrySnapshot(
    Guid Id,
    Guid SourceDocumentId,
    DateOnly Period,
    string Category,
    decimal Amount,
    bool Included,
    bool Automatic,
    int? RuleVersion);

public sealed record VatEntrySnapshot(
    Guid Id,
    Guid SourceDocumentId,
    DateOnly Period,
    decimal InputVatAmount,
    decimal DeductibleVatAmount,
    bool Included,
    bool Automatic,
    int? RuleVersion);

public interface ICostAccountingService
{
    Task<CostBookingSnapshot> PrepareAsync(
        string ownerUserId,
        Guid sourceDocumentId,
        PrepareCostCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ConfirmForDocumentAsync(
        string ownerUserId,
        Guid bookingId,
        CostDecisionCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ConfirmAndApplyFutureAsync(
        string ownerUserId,
        Guid bookingId,
        CostDecisionCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<CostReviewSnapshot?> GetReviewAsync(
        string ownerUserId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KpirEntrySnapshot>> ListKpirAsync(
        string ownerUserId,
        int year,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VatEntrySnapshot>> ListVatAsync(
        string ownerUserId,
        int year,
        CancellationToken cancellationToken = default);
}
