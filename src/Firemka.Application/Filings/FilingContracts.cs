using Firemka.Domain.Filings;

namespace Firemka.Application.Filings;

public sealed record SaveFilingProfileCommand(
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Pesel,
    string TaxOfficeCode,
    string ZusInsuranceTitleCode,
    string? Email,
    string ConfirmationEvidence,
    DateOnly ConfirmedOn);

public sealed record FilingProfileSnapshot(
    Guid Id,
    Guid CompanyId,
    int VersionNumber,
    Guid? PreviousVersionId,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Pesel,
    string TaxOfficeCode,
    string ZusInsuranceTitleCode,
    string? Email,
    string ConfirmationEvidence,
    DateOnly ConfirmedOn,
    DateTimeOffset CreatedAtUtc);

public sealed record FilingArtifactSnapshot(
    Guid Id,
    Guid MonthSettlementId,
    DateOnly Period,
    FilingArtifactKind Kind,
    int VersionNumber,
    Guid? PreviousArtifactId,
    Guid CalculationId,
    Guid FilingProfileVersionId,
    string InputFingerprint,
    string SchemaVersion,
    string GeneratorVersion,
    Guid StoredFileId,
    string FileSha256,
    FilingArtifactStatus Status,
    string? ApprovalEvidence,
    DateTimeOffset? ApprovedAtUtc,
    string? ManualSubmissionReference,
    DateTimeOffset? SentAtUtc,
    Guid? ReceiptFileId,
    string? OutcomeReference,
    DateTimeOffset? OutcomeAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record FilingWorkspace(
    Guid CompanyId,
    DateOnly Month,
    bool HasClosedSettlement,
    FilingProfileSnapshot? Profile,
    IReadOnlyList<FilingArtifactSnapshot> Artifacts);

public sealed record FilingReceiptUpload(
    string FileName,
    string MediaType,
    Stream Content);

public interface IFilingService
{
    Task<FilingProfileSnapshot?> GetProfileAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<FilingProfileSnapshot> SaveProfileAsync(
        string ownerUserId,
        Guid companyId,
        SaveFilingProfileCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FilingWorkspace> GetAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FilingArtifactSnapshot>> GenerateAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FilingArtifactSnapshot> ApproveAsync(
        string ownerUserId,
        Guid artifactId,
        string evidence,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FilingArtifactSnapshot> RecordExportAsync(
        string ownerUserId,
        Guid artifactId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FilingArtifactSnapshot> MarkSentAsync(
        string ownerUserId,
        Guid artifactId,
        string manualSubmissionReference,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<FilingArtifactSnapshot> RecordOutcomeAsync(
        string ownerUserId,
        Guid artifactId,
        FilingSubmissionOutcome outcome,
        FilingReceiptUpload receipt,
        string reference,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
