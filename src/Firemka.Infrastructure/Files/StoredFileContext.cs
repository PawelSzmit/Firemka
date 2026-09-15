namespace Firemka.Infrastructure.Files;

public enum StoredFileOrigin
{
    ManualUpload,
    KsefDownload,
    GeneratedArtifact,
    RestoredBackup,
}

public enum StoredFileRecordType
{
    SourceDocument,
    InvoiceVersion,
    FilingArtifactVersion,
    SubmissionReceipt,
    BackupRecord,
    AnnualReport,
    AnnualJpk,
}

public sealed record StoredFileContext(
    StoredFileOrigin Origin,
    StoredFileRecordType RecordType,
    Guid RecordId,
    int RecordVersion);
