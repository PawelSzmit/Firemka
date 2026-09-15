namespace Firemka.Domain.Calculations;

public enum MonthSettlementStatus
{
    OpenCorrection = 1,
    Closed = 2,
}

public sealed class MonthSettlement
{
    private MonthSettlement()
    {
    }

    private MonthSettlement(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        int versionNumber,
        Guid? previousSettlementId,
        MonthSettlementStatus status,
        string? correctionReason,
        Guid? calculationId,
        string? inputFingerprint,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? closedAtUtc)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        Month = month;
        VersionNumber = versionNumber;
        PreviousSettlementId = previousSettlementId;
        Status = status;
        CorrectionReason = correctionReason;
        CalculationId = calculationId;
        InputFingerprint = inputFingerprint;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ClosedAtUtc = closedAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public DateOnly Month { get; private set; }

    public int VersionNumber { get; private set; }

    public Guid? PreviousSettlementId { get; private set; }

    public MonthSettlementStatus Status { get; private set; }

    public string? CorrectionReason { get; private set; }

    public Guid? CalculationId { get; private set; }

    public string? InputFingerprint { get; private set; }

    public Guid ConcurrencyStamp { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static MonthSettlement CloseOriginal(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        Guid calculationId,
        string inputFingerprint,
        DateTimeOffset nowUtc)
    {
        ValidateIdentity(companyId, ownerUserId, month);
        ValidateCalculation(calculationId, inputFingerprint);
        return new MonthSettlement(
            companyId,
            ownerUserId.Trim(),
            month,
            versionNumber: 1,
            previousSettlementId: null,
            MonthSettlementStatus.Closed,
            correctionReason: null,
            calculationId,
            inputFingerprint.ToLowerInvariant(),
            nowUtc,
            nowUtc);
    }

    public static MonthSettlement StartCorrection(
        MonthSettlement previousClosed,
        string reason,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(previousClosed);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (previousClosed.Status != MonthSettlementStatus.Closed)
        {
            throw new InvalidOperationException("Korektę można rozpocząć tylko od zamkniętej wersji miesiąca.");
        }

        return new MonthSettlement(
            previousClosed.CompanyId,
            previousClosed.OwnerUserId,
            previousClosed.Month,
            previousClosed.VersionNumber + 1,
            previousClosed.Id,
            MonthSettlementStatus.OpenCorrection,
            reason.Trim(),
            calculationId: null,
            inputFingerprint: null,
            nowUtc,
            closedAtUtc: null);
    }

    public void Close(Guid calculationId, string inputFingerprint, DateTimeOffset nowUtc)
    {
        if (Status != MonthSettlementStatus.OpenCorrection)
        {
            throw new InvalidOperationException("Ta wersja miesiąca jest już zamknięta.");
        }

        ValidateCalculation(calculationId, inputFingerprint);
        CalculationId = calculationId;
        InputFingerprint = inputFingerprint.ToLowerInvariant();
        Status = MonthSettlementStatus.Closed;
        ClosedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static void ValidateIdentity(Guid companyId, string ownerUserId, DateOnly month)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (month.Day != 1)
        {
            throw new ArgumentException("Miesiąc zamknięcia musi zaczynać się pierwszego dnia.", nameof(month));
        }
    }

    private static void ValidateCalculation(Guid calculationId, string inputFingerprint)
    {
        if (calculationId == Guid.Empty)
        {
            throw new ArgumentException("Kalkulacja jest wymagana.", nameof(calculationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(inputFingerprint);
        if (inputFingerprint.Length != 64 || inputFingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Odcisk wejścia musi być 64-znakowym SHA-256.", nameof(inputFingerprint));
        }
    }
}
