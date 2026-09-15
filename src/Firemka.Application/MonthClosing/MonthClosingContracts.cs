using Firemka.Domain.Calculations;

namespace Firemka.Application.MonthClosing;

public static class MonthClosingBlockerCodes
{
    public const string TaxYearMissing = "TAX_YEAR_MISSING";
    public const string VatProfileUnsupported = "VAT_PROFILE_UNSUPPORTED";
    public const string ZusProfileUnsupported = "ZUS_PROFILE_UNSUPPORTED";
    public const string RuleSetMissing = "RULE_SET_MISSING";
    public const string RuleSetUnconfirmed = "RULE_SET_UNCONFIRMED";
    public const string DeclarationMissing = "DECLARATION_MISSING";
    public const string OpeningBalancesUnconfirmed = "OPENING_BALANCES_UNCONFIRMED";
    public const string HealthInputUnconfirmed = "HEALTH_INPUT_UNCONFIRMED";
    public const string PreviousMonthOpen = "PREVIOUS_MONTH_OPEN";
    public const string MonthNotEnded = "MONTH_NOT_ENDED";
    public const string SourceDocumentUnresolved = "SOURCE_DOCUMENT_UNRESOLVED";
    public const string CostBookingPending = "COST_BOOKING_PENDING";
    public const string SalesInvoiceMissing = "SALES_INVOICE_MISSING";
    public const string SalesInvoiceNotFinal = "SALES_INVOICE_NOT_FINAL";
    public const string SalesContentMismatch = "SALES_CONTENT_MISMATCH";
    public const string LateSalesRecognitionUnresolved = "LATE_SALES_RECOGNITION_UNRESOLVED";
    public const string ForeignServiceVatUnresolved = "FOREIGN_SERVICE_VAT_UNRESOLVED";
    public const string ClosedInputDrift = "CLOSED_INPUT_DRIFT";
    public const string CorrectionRequired = "CORRECTION_REQUIRED";
}

public sealed record MonthClosingBlocker(
    string Code,
    string Message,
    string ActionUrl);

public sealed record ConfirmCalculationRuleSetCommand(
    bool IndependentVerificationConfirmed,
    string EvidenceReference,
    DateOnly ConfirmedOn);

public sealed record SaveMonthDeclarationCommand(
    decimal SocialContributionsDeductible,
    decimal PitBaseAdjustment,
    decimal HealthIncomeAdjustment,
    decimal? OpeningVatCarryForward,
    decimal? OpeningPitAdvancesDue,
    bool OpeningBalancesConfirmed,
    bool HealthIncomeConfirmed,
    string EvidenceReference);

public sealed record AddMonthAdjustmentCommand(
    MonthAdjustmentKind Kind,
    decimal Amount,
    string Reason,
    string EvidenceReference,
    Guid? SourceDocumentId,
    Guid? SalesInvoiceId);

public sealed record CalculationRuleSetSnapshot(
    Guid Id,
    Guid CompanyId,
    int TaxYear,
    int VersionNumber,
    Guid? PreviousRuleSetId,
    CalculationRuleTrust Trust,
    CalculationRuleValues Values,
    string OfficialSources,
    DateOnly CapturedOn,
    string? IndependentEvidenceReference,
    DateOnly? ConfirmedOn,
    string RuleFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record MonthDeclarationSnapshot(
    Guid Id,
    Guid CompanyId,
    DateOnly Month,
    int VersionNumber,
    Guid? PreviousDeclarationId,
    decimal SocialContributionsDeductible,
    decimal PitBaseAdjustment,
    decimal HealthIncomeAdjustment,
    decimal? OpeningVatCarryForward,
    decimal? OpeningPitAdvancesDue,
    bool OpeningBalancesConfirmed,
    bool HealthIncomeConfirmed,
    string EvidenceReference,
    string ValueFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record MonthAdjustmentSnapshot(
    Guid Id,
    Guid CompanyId,
    DateOnly Month,
    MonthAdjustmentKind Kind,
    decimal Amount,
    string Reason,
    string EvidenceReference,
    Guid? SourceDocumentId,
    Guid? SalesInvoiceId,
    string ValueFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record MonthCalculationLineSnapshot(
    int Sequence,
    string Code,
    string Label,
    decimal Amount,
    string SourceReference,
    bool IsFormula);

public sealed record MonthCalculationSnapshot(
    Guid Id,
    Guid CompanyId,
    DateOnly Month,
    int VersionNumber,
    Guid RuleSetId,
    Guid DeclarationId,
    Guid? PreviousMonthCalculationId,
    Guid? PreviousVersionId,
    string InputFingerprint,
    CalculationTrust Trust,
    decimal RevenueMonth,
    decimal CostsMonth,
    decimal SocialContributionsMonth,
    decimal PitBaseAdjustmentMonth,
    decimal HealthIncomeAdjustmentMonth,
    decimal RevenueYtd,
    decimal CostsYtd,
    decimal SocialContributionsYtd,
    decimal PitAdjustmentsYtd,
    decimal PitIncomeYtd,
    decimal PitTaxBase,
    decimal CumulativePitTax,
    decimal PriorPitAdvancesDue,
    decimal PitAdvanceDue,
    bool CanDeferPitPayment,
    decimal OutputVatMonth,
    decimal InputVatMonth,
    decimal PriorVatCarryForward,
    decimal VatPayable,
    decimal VatPayableRounded,
    decimal VatCarryForward,
    decimal CurrentHealthIncome,
    decimal PreviousMonthHealthIncome,
    decimal HealthMinimumBase,
    decimal HealthBasis,
    decimal HealthContribution,
    IReadOnlyList<MonthCalculationLineSnapshot> Lines,
    DateTimeOffset CreatedAtUtc);

public sealed record MonthSettlementSnapshot(
    Guid Id,
    Guid CompanyId,
    DateOnly Month,
    int VersionNumber,
    Guid? PreviousSettlementId,
    MonthSettlementStatus Status,
    string? CorrectionReason,
    Guid? CalculationId,
    string? InputFingerprint,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc);

public sealed record MonthClosingView(
    Guid CompanyId,
    string CompanyName,
    DateOnly Month,
    bool IsFirstBusinessMonth,
    CalculationRuleSetSnapshot? RuleSet,
    MonthDeclarationSnapshot? Declaration,
    IReadOnlyList<MonthAdjustmentSnapshot> Adjustments,
    MonthCalculationSnapshot? Calculation,
    IReadOnlyList<MonthCalculationSnapshot> CalculationHistory,
    MonthSettlementSnapshot? LatestSettlement,
    IReadOnlyList<MonthSettlementSnapshot> SettlementHistory,
    IReadOnlyList<MonthClosingBlocker> Blockers,
    bool HasInputDrift);

public sealed class MonthClosingBlockedException(IReadOnlyList<MonthClosingBlocker> blockers)
    : InvalidOperationException(
        blockers.Count == 0
            ? "Nie można zamknąć miesiąca."
            : $"Nie można zamknąć miesiąca: {blockers[0].Message}")
{
    public IReadOnlyList<MonthClosingBlocker> Blockers { get; } = blockers;
}

public interface IMonthClosingService
{
    Task<MonthClosingView?> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<CalculationRuleSetSnapshot> ConfirmRuleSetAsync(
        string ownerUserId,
        Guid ruleSetId,
        ConfirmCalculationRuleSetCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<MonthDeclarationSnapshot> SaveDeclarationAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        SaveMonthDeclarationCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<MonthAdjustmentSnapshot> AddAdjustmentAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        AddMonthAdjustmentCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<MonthSettlementSnapshot> CloseAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<MonthSettlementSnapshot> StartCorrectionAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        string reason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
