using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Firemka.Domain.Calculations;

public sealed class MonthDeclaration
{
    private MonthDeclaration()
    {
    }

    private MonthDeclaration(
        Guid id,
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        int versionNumber,
        Guid? previousDeclarationId,
        decimal socialContributionsDeductible,
        decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment,
        decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue,
        bool openingBalancesConfirmed,
        bool healthIncomeConfirmed,
        string evidenceReference,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        Month = month;
        VersionNumber = versionNumber;
        PreviousDeclarationId = previousDeclarationId;
        SocialContributionsDeductible = socialContributionsDeductible;
        PitBaseAdjustment = pitBaseAdjustment;
        HealthIncomeAdjustment = healthIncomeAdjustment;
        OpeningVatCarryForward = openingVatCarryForward;
        OpeningPitAdvancesDue = openingPitAdvancesDue;
        OpeningBalancesConfirmed = openingBalancesConfirmed;
        HealthIncomeConfirmed = healthIncomeConfirmed;
        EvidenceReference = evidenceReference;
        CreatedAtUtc = createdAtUtc;
        ValueFingerprint = CreateFingerprint(
            socialContributionsDeductible,
            pitBaseAdjustment,
            healthIncomeAdjustment,
            openingVatCarryForward,
            openingPitAdvancesDue,
            openingBalancesConfirmed,
            healthIncomeConfirmed,
            evidenceReference);
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public DateOnly Month { get; private set; }

    public int VersionNumber { get; private set; }

    public Guid? PreviousDeclarationId { get; private set; }

    public decimal SocialContributionsDeductible { get; private set; }

    public decimal PitBaseAdjustment { get; private set; }

    public decimal HealthIncomeAdjustment { get; private set; }

    public decimal? OpeningVatCarryForward { get; private set; }

    public decimal? OpeningPitAdvancesDue { get; private set; }

    public bool OpeningBalancesConfirmed { get; private set; }

    public bool HealthIncomeConfirmed { get; private set; }

    public string EvidenceReference { get; private set; } = string.Empty;

    public string ValueFingerprint { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static MonthDeclaration Create(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        decimal socialContributionsDeductible,
        decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment,
        decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue,
        bool openingBalancesConfirmed,
        bool healthIncomeConfirmed,
        string evidenceReference,
        DateTimeOffset nowUtc)
        => CreateVersion(
            companyId,
            ownerUserId,
            month,
            versionNumber: 1,
            previousDeclarationId: null,
            socialContributionsDeductible,
            pitBaseAdjustment,
            healthIncomeAdjustment,
            openingVatCarryForward,
            openingPitAdvancesDue,
            openingBalancesConfirmed,
            healthIncomeConfirmed,
            evidenceReference,
            nowUtc);

    public MonthDeclaration CreateRevision(
        decimal socialContributionsDeductible,
        decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment,
        decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue,
        bool openingBalancesConfirmed,
        bool healthIncomeConfirmed,
        string evidenceReference,
        DateTimeOffset nowUtc)
        => CreateVersion(
            CompanyId,
            OwnerUserId,
            Month,
            VersionNumber + 1,
            Id,
            socialContributionsDeductible,
            pitBaseAdjustment,
            healthIncomeAdjustment,
            openingVatCarryForward,
            openingPitAdvancesDue,
            openingBalancesConfirmed,
            healthIncomeConfirmed,
            evidenceReference,
            nowUtc);

    private static MonthDeclaration CreateVersion(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        int versionNumber,
        Guid? previousDeclarationId,
        decimal socialContributionsDeductible,
        decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment,
        decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue,
        bool openingBalancesConfirmed,
        bool healthIncomeConfirmed,
        string evidenceReference,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        if (month.Day != 1)
        {
            throw new ArgumentException("Deklarowany miesiąc musi zaczynać się pierwszego dnia.", nameof(month));
        }

        if (versionNumber <= 0
            || (versionNumber == 1 && previousDeclarationId is not null)
            || (versionNumber > 1 && previousDeclarationId is null))
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Wersja deklaracji ma niespójnego poprzednika.");
        }

        ValidateNonNegative(socialContributionsDeductible, nameof(socialContributionsDeductible));
        ValidateNullableNonNegative(openingVatCarryForward, nameof(openingVatCarryForward));
        ValidateNullableNonNegative(openingPitAdvancesDue, nameof(openingPitAdvancesDue));
        if (openingBalancesConfirmed
            && (openingVatCarryForward is null || openingPitAdvancesDue is null))
        {
            throw new InvalidOperationException(
                "Potwierdzone salda otwarcia wymagają jawnej wartości VAT i zaliczek PIT, także gdy wynosi zero.");
        }

        return new MonthDeclaration(
            Guid.NewGuid(),
            companyId,
            ownerUserId.Trim(),
            month,
            versionNumber,
            previousDeclarationId,
            RoundMoney(socialContributionsDeductible),
            RoundMoney(pitBaseAdjustment),
            RoundMoney(healthIncomeAdjustment),
            openingVatCarryForward is null ? null : RoundMoney(openingVatCarryForward.Value),
            openingPitAdvancesDue is null ? null : RoundMoney(openingPitAdvancesDue.Value),
            openingBalancesConfirmed,
            healthIncomeConfirmed,
            evidenceReference.Trim(),
            nowUtc);
    }

    private static void ValidateNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Wartość nie może być ujemna.");
        }
    }

    private static void ValidateNullableNonNegative(decimal? value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Wartość nie może być ujemna.");
        }
    }

    private static string CreateFingerprint(
        decimal socialContributionsDeductible,
        decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment,
        decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue,
        bool openingBalancesConfirmed,
        bool healthIncomeConfirmed,
        string evidenceReference)
    {
        var canonical = string.Join(
            '|',
            socialContributionsDeductible.ToString("G29", CultureInfo.InvariantCulture),
            pitBaseAdjustment.ToString("G29", CultureInfo.InvariantCulture),
            healthIncomeAdjustment.ToString("G29", CultureInfo.InvariantCulture),
            openingVatCarryForward?.ToString("G29", CultureInfo.InvariantCulture) ?? "null",
            openingPitAdvancesDue?.ToString("G29", CultureInfo.InvariantCulture) ?? "null",
            openingBalancesConfirmed ? "1" : "0",
            healthIncomeConfirmed ? "1" : "0",
            evidenceReference);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
