namespace Firemka.Domain.Filings;

public enum FilingArtifactKind
{
    JpkV7M3 = 1,
    JpkPkpir3 = 2,
    ZusDraKedu227 = 3,
}

public enum FilingArtifactStatus
{
    ApprovalRequired = 1,
    Approved = 2,
    Sent = 3,
    Accepted = 4,
    Rejected = 5,
}

public enum FilingSubmissionOutcome
{
    Accepted = 1,
    Rejected = 2,
}

public sealed class FilingArtifact
{
    private FilingArtifact()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public Guid MonthSettlementId { get; private set; }
    public DateOnly Period { get; private set; }
    public FilingArtifactKind Kind { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousArtifactId { get; private set; }
    public Guid CalculationId { get; private set; }
    public Guid FilingProfileVersionId { get; private set; }
    public string InputFingerprint { get; private set; } = string.Empty;
    public string SchemaVersion { get; private set; } = string.Empty;
    public string GeneratorVersion { get; private set; } = string.Empty;
    public Guid StoredFileId { get; private set; }
    public string FileSha256 { get; private set; } = string.Empty;
    public FilingArtifactStatus Status { get; private set; }
    public string? ApprovalEvidence { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? ManualSubmissionReference { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public Guid? ReceiptFileId { get; private set; }
    public string? OutcomeReference { get; private set; }
    public DateTimeOffset? OutcomeAtUtc { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FilingArtifact Prepare(
        Guid companyId,
        string ownerUserId,
        Guid monthSettlementId,
        DateOnly period,
        FilingArtifactKind kind,
        int versionNumber,
        Guid? previousArtifactId,
        Guid calculationId,
        Guid filingProfileVersionId,
        string inputFingerprint,
        string schemaVersion,
        string generatorVersion,
        Guid storedFileId,
        string fileSha256,
        DateTimeOffset createdAtUtc,
        Guid? artifactId = null)
    {
        if (companyId == Guid.Empty || monthSettlementId == Guid.Empty
            || calculationId == Guid.Empty || filingProfileVersionId == Guid.Empty
            || storedFileId == Guid.Empty)
        {
            throw new ArgumentException("Firma, zamknięcie, kalkulacja, profil i plik są wymagane.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorVersion);
        if (artifactId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator dokumentu nie może być pusty.", nameof(artifactId));
        }

        if (schemaVersion.Trim().Length > 100)
        {
            throw new ArgumentException("Opis wersji schematu może mieć maksymalnie 100 znaków.", nameof(schemaVersion));
        }
        if (generatorVersion.Trim().Length > 64)
        {
            throw new ArgumentException("Wersja generatora może mieć maksymalnie 64 znaki.", nameof(generatorVersion));
        }
        ValidateSha256(inputFingerprint, nameof(inputFingerprint));
        ValidateSha256(fileSha256, nameof(fileSha256));
        if (period.Day != 1)
        {
            throw new ArgumentException("Okres dokumentu musi zaczynać się pierwszego dnia miesiąca.", nameof(period));
        }

        if (!Enum.IsDefined(kind) || versionNumber <= 0
            || (versionNumber == 1 && previousArtifactId is not null)
            || (versionNumber > 1 && previousArtifactId is null))
        {
            throw new ArgumentException("Łańcuch wersji dokumentu jest nieprawidłowy.");
        }

        return new FilingArtifact
        {
            Id = artifactId ?? Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            MonthSettlementId = monthSettlementId,
            Period = period,
            Kind = kind,
            VersionNumber = versionNumber,
            PreviousArtifactId = previousArtifactId,
            CalculationId = calculationId,
            FilingProfileVersionId = filingProfileVersionId,
            InputFingerprint = inputFingerprint.ToLowerInvariant(),
            SchemaVersion = schemaVersion.Trim(),
            GeneratorVersion = generatorVersion.Trim(),
            StoredFileId = storedFileId,
            FileSha256 = fileSha256.ToUpperInvariant(),
            Status = FilingArtifactStatus.ApprovalRequired,
            ConcurrencyStamp = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc,
        };
    }

    public void Approve(string evidence, DateTimeOffset occurredAtUtc)
    {
        EnsureStatus(FilingArtifactStatus.ApprovalRequired);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (evidence.Trim().Length > 2_000)
        {
            throw new ArgumentException("Opis kontroli może mieć maksymalnie 2000 znaków.", nameof(evidence));
        }

        ApprovalEvidence = evidence.Trim();
        ApprovedAtUtc = occurredAtUtc;
        Status = FilingArtifactStatus.Approved;
        RotateConcurrencyStamp();
    }

    public void MarkSent(string manualSubmissionReference, DateTimeOffset occurredAtUtc)
    {
        EnsureStatus(FilingArtifactStatus.Approved);
        if (Kind == FilingArtifactKind.JpkPkpir3)
        {
            throw new InvalidOperationException(
                "JPK_PKPIR można oznaczyć jako wysłany dopiero po zamknięciu roku.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(manualSubmissionReference);
        if (manualSubmissionReference.Trim().Length > 2_000)
        {
            throw new ArgumentException("Referencja wysyłki może mieć maksymalnie 2000 znaków.", nameof(manualSubmissionReference));
        }
        ManualSubmissionReference = manualSubmissionReference.Trim();
        SentAtUtc = occurredAtUtc;
        Status = FilingArtifactStatus.Sent;
        RotateConcurrencyStamp();
    }

    public void RecordOutcome(
        FilingSubmissionOutcome outcome,
        Guid receiptFileId,
        string reference,
        DateTimeOffset occurredAtUtc)
    {
        EnsureStatus(FilingArtifactStatus.Sent);
        if (!Enum.IsDefined(outcome) || receiptFileId == Guid.Empty)
        {
            throw new ArgumentException("Wynik i plik potwierdzenia są wymagane.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (reference.Trim().Length > 2_000)
        {
            throw new ArgumentException("Referencja wyniku może mieć maksymalnie 2000 znaków.", nameof(reference));
        }
        ReceiptFileId = receiptFileId;
        OutcomeReference = reference.Trim();
        OutcomeAtUtc = occurredAtUtc;
        Status = outcome == FilingSubmissionOutcome.Accepted
            ? FilingArtifactStatus.Accepted
            : FilingArtifactStatus.Rejected;
        RotateConcurrencyStamp();
    }

    private void EnsureStatus(FilingArtifactStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Ta czynność wymaga stanu {expected}, a dokument ma stan {Status}.");
        }
    }

    private void RotateConcurrencyStamp() => ConcurrencyStamp = Guid.NewGuid();

    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Odcisk musi być 64-znakowym SHA-256.", parameterName);
        }
    }
}
