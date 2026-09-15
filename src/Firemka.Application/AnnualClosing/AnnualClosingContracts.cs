using Firemka.Application.Filings;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Filings;

namespace Firemka.Application.AnnualClosing;

public static class AnnualClosingBlockerCodes
{
    public const string YearNotEnded = "YEAR_NOT_ENDED";
    public const string DeclarationMissing = "ANNUAL_DECLARATION_MISSING";
    public const string FilingProfileMissing = "FILING_PROFILE_MISSING";
    public const string MonthMissing = "MONTH_MISSING";
    public const string MonthOpen = "MONTH_OPEN";
    public const string MonthDrift = "MONTH_INPUT_DRIFT";
    public const string HealthRuleMismatch = "HEALTH_RULE_MISMATCH";
    public const string CorrectionRequired = "ANNUAL_CORRECTION_REQUIRED";
}

public sealed record AnnualClosingBlocker(string Code, string Message, string ActionUrl);

public sealed record SaveAnnualDeclarationCommand(
    decimal OpeningInventory,
    decimal ClosingInventory,
    decimal PitAdvancesPaid,
    decimal HealthContributionsPaid,
    bool IndependentVerificationConfirmed,
    string EvidenceReference,
    DateOnly ConfirmedOn);

public sealed record ConfirmAnnualClosingCommand(bool IndependentResultConfirmed);

public sealed record AnnualDeclarationSnapshot(
    Guid Id, int TaxYear, int VersionNumber, Guid? PreviousDeclarationId,
    decimal OpeningInventory, decimal ClosingInventory, decimal PitAdvancesPaid,
    decimal HealthContributionsPaid, string EvidenceReference, DateOnly ConfirmedOn,
    DateTimeOffset CreatedAtUtc);

public sealed record AnnualMonthSnapshot(
    DateOnly Month, Guid MonthSettlementId, Guid MonthCalculationId,
    decimal Revenue, decimal Costs, decimal PitAdvanceDue,
    decimal HealthIncome, decimal HealthContributionDue);

public sealed record AnnualClosingSnapshot(
    Guid Id, int TaxYear, int VersionNumber, Guid? PreviousClosingId,
    AnnualClosingStatus Status, string? CorrectionReason, Guid? DeclarationId,
    AnnualClosingValues Values, IReadOnlyList<AnnualMonthSnapshot> Months,
    Guid? JpkStoredFileId, string? JpkSha256, string? JpkGeneratorVersion,
    FilingArtifactStatus JpkStatus, string? JpkApprovalEvidence, DateTimeOffset? JpkApprovedAtUtc,
    string? JpkManualSubmissionReference, DateTimeOffset? JpkSentAtUtc,
    Guid? JpkReceiptStoredFileId, string? JpkOutcomeReference, DateTimeOffset? JpkOutcomeAtUtc,
    Guid? PdfStoredFileId, string? PdfSha256, string? PdfGeneratorVersion,
    string? InputFingerprint, bool FinalResultConfirmed,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ClosedAtUtc);

public sealed record AnnualClosingView(
    Guid CompanyId, string CompanyName, string Nip, int TaxYear,
    AnnualDeclarationSnapshot? Declaration, AnnualClosingValues? Preview,
    IReadOnlyList<AnnualMonthSnapshot> Months, AnnualClosingSnapshot? LatestClosing,
    IReadOnlyList<AnnualClosingSnapshot> ClosingHistory,
    IReadOnlyList<AnnualClosingBlocker> Blockers);

public sealed record AnnualReportPdfInput(
    string CompanyName, string Nip, string Address, string OwnerName,
    int TaxYear, int VersionNumber, AnnualClosingValues Values,
    IReadOnlyList<AnnualMonthSnapshot> Months, string EvidenceReference,
    DateOnly ConfirmedOn, DateTimeOffset GeneratedAtUtc, string OfficialSources);

public interface IAnnualReportPdfGenerator
{
    byte[] Generate(AnnualReportPdfInput input);
}

public interface IAnnualArchiveRequestQueue
{
    void Stage(Guid companyId, string ownerUserId, int taxYear, Guid annualClosingId, DateTimeOffset nowUtc);
}

public interface IAnnualClosingService
{
    Task<AnnualClosingView> GetAsync(string ownerUserId, Guid companyId, int taxYear,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualDeclarationSnapshot> SaveDeclarationAsync(string ownerUserId, Guid companyId, int taxYear,
        SaveAnnualDeclarationCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualClosingSnapshot> CloseAsync(string ownerUserId, Guid companyId, int taxYear,
        ConfirmAnnualClosingCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualClosingSnapshot> StartCorrectionAsync(string ownerUserId, Guid companyId, int taxYear,
        string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualClosingSnapshot> ApproveJpkAsync(string ownerUserId, Guid annualClosingId,
        string evidence, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualClosingSnapshot> MarkJpkSentAsync(string ownerUserId, Guid annualClosingId,
        string manualSubmissionReference, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<AnnualClosingSnapshot> RecordJpkOutcomeAsync(string ownerUserId, Guid annualClosingId,
        FilingSubmissionOutcome outcome, FilingReceiptUpload receipt, string reference,
        DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public sealed class AnnualClosingBlockedException(IReadOnlyList<AnnualClosingBlocker> blockers)
    : InvalidOperationException(blockers.Count == 0
        ? "Nie można zamknąć roku."
        : $"Nie można zamknąć roku: {blockers[0].Message}")
{
    public IReadOnlyList<AnnualClosingBlocker> Blockers { get; } = blockers;
}
