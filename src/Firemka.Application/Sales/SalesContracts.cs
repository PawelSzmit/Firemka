using Firemka.Domain.Sales;

namespace Firemka.Application.Sales;

public sealed record SalesInvoiceSummary(
    Guid Id,
    DateOnly ServiceMonth,
    DateOnly ServicePeriodFrom,
    DateOnly ServicePeriodTo,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount,
    string Currency,
    SalesInvoiceStatus Status,
    bool HasManualAmountOverride,
    bool HasPendingSubscriptionRate,
    DateOnly? IssueDate,
    DateOnly? PaymentDueDate,
    string? InvoiceNumber,
    string? KsefNumber,
    string? RejectionReason);

public sealed record SalesInvoiceDetails(
    SalesInvoiceSummary Invoice,
    string Description,
    decimal VatRate,
    decimal? PendingSubscriptionNetAmount,
    decimal? PendingSubscriptionVatRate,
    bool IsLateApproval,
    bool OutgoingKsefEnabled,
    bool AutomationEnabled,
    bool AutomationCanBeEnabled);

public sealed record SalesAutomationSnapshot(
    bool Enabled,
    bool CanBeEnabled,
    DateTimeOffset? ChangedAtUtc);

public sealed record IssueSalesInvoiceResult(
    SalesInvoiceStatus Status,
    bool IsLateApproval,
    string Message,
    string? KsefNumber);

public interface ISalesInvoiceService
{
    Task<SalesInvoiceSummary> EnsureDraftAsync(
        string ownerUserId,
        DateOnly serviceMonth,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummary>> ListAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task<SalesInvoiceDetails?> GetAsync(
        string ownerUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task EditDraftAsync(
        string ownerUserId,
        Guid invoiceId,
        decimal netAmount,
        decimal vatRate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ApplyPendingSubscriptionRateAsync(
        string ownerUserId,
        Guid invoiceId,
        bool replaceManualOverride,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ReopenRejectedAsync(
        string ownerUserId,
        Guid invoiceId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IssueSalesInvoiceResult> IssueAsync(
        string ownerUserId,
        Guid invoiceId,
        bool automatic,
        DateOnly actualIssueDate,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<SalesAutomationSnapshot> GetAutomationAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task SetAutomationAsync(
        string ownerUserId,
        bool enabled,
        bool warningAcknowledged,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record Fa3InvoiceInput(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTimeOffset GeneratedAtUtc,
    DateOnly IssueDate,
    DateOnly ServicePeriodFrom,
    DateOnly ServicePeriodTo,
    string SellerName,
    string SellerNip,
    string SellerAddress,
    string BuyerName,
    string BuyerNip,
    string BuyerAddress,
    string Description,
    decimal NetAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal GrossAmount,
    DateOnly PaymentDueDate,
    string Currency);

public interface IFa3InvoiceGenerator
{
    string Generate(Fa3InvoiceInput input);

    void Validate(string xml);
}
