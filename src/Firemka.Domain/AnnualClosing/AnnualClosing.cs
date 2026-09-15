using Firemka.Domain.Filings;

namespace Firemka.Domain.AnnualClosing;

public enum AnnualClosingStatus
{
    OpenCorrection = 1,
    Closed = 2,
}

public sealed record AnnualClosingValues(
    decimal Revenue,
    decimal CostsBeforeInventory,
    decimal OpeningInventory,
    decimal ClosingInventory,
    decimal CostsAfterInventory,
    decimal SocialContributions,
    decimal PitAdjustments,
    decimal PitIncome,
    decimal PitAdvancesDue,
    decimal PitAdvancesPaid,
    decimal HealthIncome,
    decimal AnnualHealthMinimumBase,
    decimal AnnualHealthBasis,
    decimal AnnualHealthContributionDue,
    decimal HealthContributionsDueMonthly,
    decimal HealthContributionsPaid,
    decimal HealthSettlementDifference,
    decimal HealthPaymentDifference);

public sealed record AnnualClosingMonthInput(
    DateOnly Month,
    Guid MonthSettlementId,
    Guid MonthCalculationId,
    decimal Revenue,
    decimal Costs,
    decimal PitAdvanceDue,
    decimal HealthIncome,
    decimal HealthContributionDue);

public sealed class AnnualClosingMonth
{
    private AnnualClosingMonth()
    {
    }

    public Guid Id { get; private set; }
    public Guid AnnualClosingId { get; private set; }
    public DateOnly Month { get; private set; }
    public Guid MonthSettlementId { get; private set; }
    public Guid MonthCalculationId { get; private set; }
    public decimal Revenue { get; private set; }
    public decimal Costs { get; private set; }
    public decimal PitAdvanceDue { get; private set; }
    public decimal HealthIncome { get; private set; }
    public decimal HealthContributionDue { get; private set; }

    internal static AnnualClosingMonth Create(Guid closingId, AnnualClosingMonthInput input)
    {
        if (input.MonthSettlementId == Guid.Empty || input.MonthCalculationId == Guid.Empty)
        {
            throw new ArgumentException("Zamknięcie i kalkulacja miesiąca są wymagane.", nameof(input));
        }
        return new AnnualClosingMonth
        {
            Id = Guid.NewGuid(),
            AnnualClosingId = closingId,
            Month = input.Month,
            MonthSettlementId = input.MonthSettlementId,
            MonthCalculationId = input.MonthCalculationId,
            Revenue = decimal.Round(input.Revenue, 2),
            Costs = decimal.Round(input.Costs, 2),
            PitAdvanceDue = decimal.Round(input.PitAdvanceDue, 2),
            HealthIncome = decimal.Round(input.HealthIncome, 2),
            HealthContributionDue = decimal.Round(input.HealthContributionDue, 2),
        };
    }
}

public sealed class AnnualClosing
{
    private readonly List<AnnualClosingMonth> _months = [];

    private AnnualClosing()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public int TaxYear { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousClosingId { get; private set; }
    public AnnualClosingStatus Status { get; private set; }
    public string? CorrectionReason { get; private set; }
    public Guid? DeclarationId { get; private set; }
    public decimal Revenue { get; private set; }
    public decimal CostsBeforeInventory { get; private set; }
    public decimal OpeningInventory { get; private set; }
    public decimal ClosingInventory { get; private set; }
    public decimal CostsAfterInventory { get; private set; }
    public decimal SocialContributions { get; private set; }
    public decimal PitAdjustments { get; private set; }
    public decimal PitIncome { get; private set; }
    public decimal PitAdvancesDue { get; private set; }
    public decimal PitAdvancesPaid { get; private set; }
    public decimal HealthIncome { get; private set; }
    public decimal AnnualHealthMinimumBase { get; private set; }
    public decimal AnnualHealthBasis { get; private set; }
    public decimal AnnualHealthContributionDue { get; private set; }
    public decimal HealthContributionsDueMonthly { get; private set; }
    public decimal HealthContributionsPaid { get; private set; }
    public decimal HealthSettlementDifference { get; private set; }
    public decimal HealthPaymentDifference { get; private set; }
    public Guid? JpkStoredFileId { get; private set; }
    public string? JpkSha256 { get; private set; }
    public string? JpkGeneratorVersion { get; private set; }
    public FilingArtifactStatus JpkStatus { get; private set; }
    public string? JpkApprovalEvidence { get; private set; }
    public DateTimeOffset? JpkApprovedAtUtc { get; private set; }
    public string? JpkManualSubmissionReference { get; private set; }
    public DateTimeOffset? JpkSentAtUtc { get; private set; }
    public Guid? JpkReceiptStoredFileId { get; private set; }
    public string? JpkOutcomeReference { get; private set; }
    public DateTimeOffset? JpkOutcomeAtUtc { get; private set; }
    public Guid? PdfStoredFileId { get; private set; }
    public string? PdfSha256 { get; private set; }
    public string? PdfGeneratorVersion { get; private set; }
    public string? InputFingerprint { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public bool FinalResultConfirmed { get; private set; }
    public IReadOnlyCollection<AnnualClosingMonth> Months => _months.AsReadOnly();
    public AnnualClosingValues Values => new(
        Revenue, CostsBeforeInventory, OpeningInventory, ClosingInventory, CostsAfterInventory,
        SocialContributions, PitAdjustments, PitIncome, PitAdvancesDue, PitAdvancesPaid,
        HealthIncome, AnnualHealthMinimumBase, AnnualHealthBasis, AnnualHealthContributionDue,
        HealthContributionsDueMonthly, HealthContributionsPaid, HealthSettlementDifference,
        HealthPaymentDifference);

    public static AnnualClosing CloseOriginal(
        Guid companyId, string ownerUserId, int taxYear, Guid declarationId,
        AnnualClosingValues values, IReadOnlyCollection<AnnualClosingMonthInput> months,
        Guid jpkStoredFileId, string jpkSha256, string jpkGeneratorVersion,
        Guid pdfStoredFileId, string pdfSha256, string pdfGeneratorVersion,
        string inputFingerprint, bool finalResultConfirmed, DateTimeOffset nowUtc)
    {
        ValidateIdentity(companyId, ownerUserId, taxYear);
        var closing = new AnnualClosing
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            TaxYear = taxYear,
            VersionNumber = 1,
            Status = AnnualClosingStatus.Closed,
            CreatedAtUtc = nowUtc,
            ClosedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
        closing.ApplyClose(declarationId, values, months, jpkStoredFileId, jpkSha256, jpkGeneratorVersion,
            pdfStoredFileId, pdfSha256, pdfGeneratorVersion, inputFingerprint, finalResultConfirmed, nowUtc);
        return closing;
    }

    public static AnnualClosing StartCorrection(AnnualClosing previousClosed, string reason, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(previousClosed);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (previousClosed.Status != AnnualClosingStatus.Closed)
        {
            throw new InvalidOperationException("Korektę można rozpocząć tylko od zamkniętej wersji roku.");
        }
        if (reason.Trim().Length > 2_000)
        {
            throw new ArgumentException("Powód korekty może mieć maksymalnie 2000 znaków.", nameof(reason));
        }
        return new AnnualClosing
        {
            Id = Guid.NewGuid(),
            CompanyId = previousClosed.CompanyId,
            OwnerUserId = previousClosed.OwnerUserId,
            TaxYear = previousClosed.TaxYear,
            VersionNumber = previousClosed.VersionNumber + 1,
            PreviousClosingId = previousClosed.Id,
            Status = AnnualClosingStatus.OpenCorrection,
            CorrectionReason = reason.Trim(),
            CreatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
    }

    public void Close(
        Guid declarationId, AnnualClosingValues values, IReadOnlyCollection<AnnualClosingMonthInput> months,
        Guid jpkStoredFileId, string jpkSha256, string jpkGeneratorVersion,
        Guid pdfStoredFileId, string pdfSha256, string pdfGeneratorVersion,
        string inputFingerprint, bool finalResultConfirmed, DateTimeOffset nowUtc)
    {
        if (Status != AnnualClosingStatus.OpenCorrection)
        {
            throw new InvalidOperationException("Ta wersja roku jest już zamknięta.");
        }
        ApplyClose(declarationId, values, months, jpkStoredFileId, jpkSha256, jpkGeneratorVersion,
            pdfStoredFileId, pdfSha256, pdfGeneratorVersion, inputFingerprint, finalResultConfirmed, nowUtc);
        Status = AnnualClosingStatus.Closed;
        ClosedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void ApproveJpk(string evidence, DateTimeOffset occurredAtUtc)
    {
        EnsureClosedJpkStatus(FilingArtifactStatus.ApprovalRequired);
        JpkApprovalEvidence = ValidateReference(evidence, nameof(evidence));
        JpkApprovedAtUtc = occurredAtUtc;
        JpkStatus = FilingArtifactStatus.Approved;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void MarkJpkSent(string manualSubmissionReference, DateTimeOffset occurredAtUtc)
    {
        EnsureClosedJpkStatus(FilingArtifactStatus.Approved);
        JpkManualSubmissionReference = ValidateReference(
            manualSubmissionReference, nameof(manualSubmissionReference));
        JpkSentAtUtc = occurredAtUtc;
        JpkStatus = FilingArtifactStatus.Sent;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void RecordJpkOutcome(
        FilingSubmissionOutcome outcome, Guid receiptStoredFileId, string reference,
        DateTimeOffset occurredAtUtc)
    {
        EnsureClosedJpkStatus(FilingArtifactStatus.Sent);
        if (!Enum.IsDefined(outcome) || receiptStoredFileId == Guid.Empty)
        {
            throw new ArgumentException("Wynik i plik potwierdzenia są wymagane.");
        }
        JpkReceiptStoredFileId = receiptStoredFileId;
        JpkOutcomeReference = ValidateReference(reference, nameof(reference));
        JpkOutcomeAtUtc = occurredAtUtc;
        JpkStatus = outcome == FilingSubmissionOutcome.Accepted
            ? FilingArtifactStatus.Accepted
            : FilingArtifactStatus.Rejected;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private void ApplyClose(
        Guid declarationId, AnnualClosingValues values, IReadOnlyCollection<AnnualClosingMonthInput> months,
        Guid jpkStoredFileId, string jpkSha256, string jpkGeneratorVersion,
        Guid pdfStoredFileId, string pdfSha256, string pdfGeneratorVersion,
        string inputFingerprint, bool finalResultConfirmed, DateTimeOffset nowUtc)
    {
        if (declarationId == Guid.Empty || jpkStoredFileId == Guid.Empty || pdfStoredFileId == Guid.Empty)
        {
            throw new ArgumentException("Deklaracja oraz pliki roczne są wymagane.");
        }
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(months);
        ValidateHash(jpkSha256, nameof(jpkSha256));
        ValidateHash(pdfSha256, nameof(pdfSha256));
        ValidateHash(inputFingerprint, nameof(inputFingerprint));
        ArgumentException.ThrowIfNullOrWhiteSpace(jpkGeneratorVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfGeneratorVersion);
        if (!finalResultConfirmed)
        {
            throw new ArgumentException("Potwierdź niezależne sprawdzenie rocznego podsumowania.", nameof(finalResultConfirmed));
        }
        if (months.Count == 0 || months.Any(item => item.Month.Year != TaxYear)
            || months.Select(item => item.Month).Distinct().Count() != months.Count)
        {
            throw new ArgumentException("Migawki miesięcy roku są nieprawidłowe.", nameof(months));
        }

        DeclarationId = declarationId;
        Revenue = values.Revenue;
        CostsBeforeInventory = values.CostsBeforeInventory;
        OpeningInventory = values.OpeningInventory;
        ClosingInventory = values.ClosingInventory;
        CostsAfterInventory = values.CostsAfterInventory;
        SocialContributions = values.SocialContributions;
        PitAdjustments = values.PitAdjustments;
        PitIncome = values.PitIncome;
        PitAdvancesDue = values.PitAdvancesDue;
        PitAdvancesPaid = values.PitAdvancesPaid;
        HealthIncome = values.HealthIncome;
        AnnualHealthMinimumBase = values.AnnualHealthMinimumBase;
        AnnualHealthBasis = values.AnnualHealthBasis;
        AnnualHealthContributionDue = values.AnnualHealthContributionDue;
        HealthContributionsDueMonthly = values.HealthContributionsDueMonthly;
        HealthContributionsPaid = values.HealthContributionsPaid;
        HealthSettlementDifference = values.HealthSettlementDifference;
        HealthPaymentDifference = values.HealthPaymentDifference;
        JpkStoredFileId = jpkStoredFileId;
        JpkSha256 = jpkSha256.ToUpperInvariant();
        JpkGeneratorVersion = jpkGeneratorVersion.Trim();
        JpkStatus = FilingArtifactStatus.ApprovalRequired;
        JpkApprovalEvidence = null;
        JpkApprovedAtUtc = null;
        JpkManualSubmissionReference = null;
        JpkSentAtUtc = null;
        JpkReceiptStoredFileId = null;
        JpkOutcomeReference = null;
        JpkOutcomeAtUtc = null;
        PdfStoredFileId = pdfStoredFileId;
        PdfSha256 = pdfSha256.ToUpperInvariant();
        PdfGeneratorVersion = pdfGeneratorVersion.Trim();
        InputFingerprint = inputFingerprint.ToLowerInvariant();
        FinalResultConfirmed = true;
        _months.Clear();
        _months.AddRange(months.OrderBy(item => item.Month).Select(item => AnnualClosingMonth.Create(Id, item)));
        ClosedAtUtc = nowUtc;
    }

    private static void ValidateIdentity(Guid companyId, string ownerUserId, int taxYear)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (taxYear is < 2000 or > 2200) throw new ArgumentOutOfRangeException(nameof(taxYear));
    }

    private static void ValidateHash(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Odcisk musi być 64-znakowym SHA-256.", parameterName);
        }
    }

    private void EnsureClosedJpkStatus(FilingArtifactStatus expected)
    {
        if (Status != AnnualClosingStatus.Closed || JpkStoredFileId is null)
        {
            throw new InvalidOperationException("Roczny JPK nie jest jeszcze gotowy.");
        }
        if (JpkStatus != expected)
        {
            throw new InvalidOperationException(
                $"Ta czynność wymaga stanu {expected}, a roczny JPK ma stan {JpkStatus}.");
        }
    }

    private static string ValidateReference(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > 2_000)
        {
            throw new ArgumentException("Opis może mieć maksymalnie 2000 znaków.", parameterName);
        }
        return value.Trim();
    }
}
