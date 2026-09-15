using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Firemka.Domain.Calculations;

public enum CalculationRuleTrust
{
    ReferenceOnly = 1,
    IndependentlyConfirmed = 2,
}

public sealed record CalculationRuleValues(
    decimal PitThreshold,
    decimal PitLowerRatePercent,
    decimal PitHigherRatePercent,
    decimal PitReducingAmount,
    decimal PitPaymentOptionThreshold,
    decimal HealthRatePercent,
    int HealthMinimumChangeMonth,
    decimal HealthMinimumBeforeChange,
    decimal HealthMinimumFromChange);

public sealed class CalculationRuleSet
{
    private CalculationRuleSet()
    {
    }

    private CalculationRuleSet(
        Guid id,
        Guid companyId,
        string ownerUserId,
        int taxYear,
        int versionNumber,
        Guid? previousRuleSetId,
        CalculationRuleValues values,
        string officialSources,
        DateOnly capturedOn,
        CalculationRuleTrust trust,
        string? independentEvidenceReference,
        DateOnly? confirmedOn,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        TaxYear = taxYear;
        VersionNumber = versionNumber;
        PreviousRuleSetId = previousRuleSetId;
        PitThreshold = values.PitThreshold;
        PitLowerRatePercent = values.PitLowerRatePercent;
        PitHigherRatePercent = values.PitHigherRatePercent;
        PitReducingAmount = values.PitReducingAmount;
        PitPaymentOptionThreshold = values.PitPaymentOptionThreshold;
        HealthRatePercent = values.HealthRatePercent;
        HealthMinimumChangeMonth = values.HealthMinimumChangeMonth;
        HealthMinimumBeforeChange = values.HealthMinimumBeforeChange;
        HealthMinimumFromChange = values.HealthMinimumFromChange;
        OfficialSources = officialSources;
        CapturedOn = capturedOn;
        Trust = trust;
        IndependentEvidenceReference = independentEvidenceReference;
        ConfirmedOn = confirmedOn;
        CreatedAtUtc = createdAtUtc;
        RuleFingerprint = CreateFingerprint(values, officialSources, capturedOn);
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public int TaxYear { get; private set; }

    public int VersionNumber { get; private set; }

    public Guid? PreviousRuleSetId { get; private set; }

    public decimal PitThreshold { get; private set; }

    public decimal PitLowerRatePercent { get; private set; }

    public decimal PitHigherRatePercent { get; private set; }

    public decimal PitReducingAmount { get; private set; }

    public decimal PitPaymentOptionThreshold { get; private set; }

    public decimal HealthRatePercent { get; private set; }

    public int HealthMinimumChangeMonth { get; private set; }

    public decimal HealthMinimumBeforeChange { get; private set; }

    public decimal HealthMinimumFromChange { get; private set; }

    public string OfficialSources { get; private set; } = string.Empty;

    public DateOnly CapturedOn { get; private set; }

    public CalculationRuleTrust Trust { get; private set; }

    public string? IndependentEvidenceReference { get; private set; }

    public DateOnly? ConfirmedOn { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string RuleFingerprint { get; private set; } = string.Empty;

    public CalculationRuleValues Values => new(
        PitThreshold,
        PitLowerRatePercent,
        PitHigherRatePercent,
        PitReducingAmount,
        PitPaymentOptionThreshold,
        HealthRatePercent,
        HealthMinimumChangeMonth,
        HealthMinimumBeforeChange,
        HealthMinimumFromChange);

    public static CalculationRuleSet CreateReference(
        Guid companyId,
        string ownerUserId,
        int taxYear,
        CalculationRuleValues values,
        string officialSources,
        DateOnly capturedOn,
        DateTimeOffset nowUtc)
    {
        ValidateIdentity(companyId, ownerUserId, taxYear);
        ArgumentNullException.ThrowIfNull(values);
        ValidateValues(values);
        var normalizedSources = NormalizeSources(officialSources);
        if (capturedOn.Year != taxYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedOn),
                "Data pozyskania zasad musi należeć do ich roku podatkowego.");
        }

        return new CalculationRuleSet(
            Guid.NewGuid(),
            companyId,
            ownerUserId.Trim(),
            taxYear,
            versionNumber: 1,
            previousRuleSetId: null,
            values,
            normalizedSources,
            capturedOn,
            CalculationRuleTrust.ReferenceOnly,
            independentEvidenceReference: null,
            confirmedOn: null,
            nowUtc);
    }

    public CalculationRuleSet Confirm(
        string independentEvidenceReference,
        DateOnly confirmedOn,
        DateTimeOffset nowUtc)
    {
        if (Trust == CalculationRuleTrust.IndependentlyConfirmed)
        {
            throw new InvalidOperationException("Ten zestaw zasad jest już niezależnie potwierdzony.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(independentEvidenceReference);
        if (confirmedOn.Year != TaxYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedOn),
                "Data niezależnego potwierdzenia musi należeć do roku podatkowego zasad.");
        }

        return new CalculationRuleSet(
            Guid.NewGuid(),
            CompanyId,
            OwnerUserId,
            TaxYear,
            VersionNumber + 1,
            Id,
            Values,
            OfficialSources,
            CapturedOn,
            CalculationRuleTrust.IndependentlyConfirmed,
            independentEvidenceReference.Trim(),
            confirmedOn,
            nowUtc);
    }

    public decimal GetMinimumHealthBase(DateOnly contributionMonth)
    {
        if (contributionMonth.Day != 1)
        {
            throw new ArgumentException("Miesiąc składki musi zaczynać się pierwszego dnia.", nameof(contributionMonth));
        }

        if (contributionMonth.Year != TaxYear)
        {
            throw new InvalidOperationException(
                $"Zestaw zasad dla roku {TaxYear} nie może obliczać miesiąca {contributionMonth:yyyy-MM}.");
        }

        return contributionMonth.Month < HealthMinimumChangeMonth
            ? HealthMinimumBeforeChange
            : HealthMinimumFromChange;
    }

    private static void ValidateIdentity(Guid companyId, string ownerUserId, int taxYear)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (taxYear is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(taxYear), "Rok podatkowy jest poza obsługiwanym zakresem.");
        }
    }

    private static void ValidateValues(CalculationRuleValues values)
    {
        ValidatePositive(values.PitThreshold, nameof(values.PitThreshold));
        ValidatePercent(values.PitLowerRatePercent, nameof(values.PitLowerRatePercent));
        ValidatePercent(values.PitHigherRatePercent, nameof(values.PitHigherRatePercent));
        ValidateNonNegative(values.PitReducingAmount, nameof(values.PitReducingAmount));
        ValidateNonNegative(values.PitPaymentOptionThreshold, nameof(values.PitPaymentOptionThreshold));
        ValidatePercent(values.HealthRatePercent, nameof(values.HealthRatePercent));
        if (values.HealthMinimumChangeMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values.HealthMinimumChangeMonth),
                "Miesiąc zmiany minimalnej podstawy musi mieścić się od 1 do 12.");
        }

        ValidatePositive(values.HealthMinimumBeforeChange, nameof(values.HealthMinimumBeforeChange));
        ValidatePositive(values.HealthMinimumFromChange, nameof(values.HealthMinimumFromChange));
    }

    private static string NormalizeSources(string officialSources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officialSources);
        var sources = officialSources
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sources.Length == 0)
        {
            throw new ArgumentException("Co najmniej jedno źródło zasad jest wymagane.", nameof(officialSources));
        }

        foreach (var source in sources)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Każde źródło zasad musi być pełnym adresem HTTPS.", nameof(officialSources));
            }
        }

        return string.Join('\n', sources.Distinct(StringComparer.Ordinal));
    }

    private static void ValidatePercent(decimal value, string parameterName)
    {
        if (value is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Procent musi mieścić się od 0 do 100.");
        }
    }

    private static void ValidatePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Wartość musi być dodatnia.");
        }
    }

    private static void ValidateNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Wartość nie może być ujemna.");
        }
    }

    private static string CreateFingerprint(
        CalculationRuleValues values,
        string officialSources,
        DateOnly capturedOn)
    {
        var canonical = string.Join(
            '|',
            values.PitThreshold.ToString("G29", CultureInfo.InvariantCulture),
            values.PitLowerRatePercent.ToString("G29", CultureInfo.InvariantCulture),
            values.PitHigherRatePercent.ToString("G29", CultureInfo.InvariantCulture),
            values.PitReducingAmount.ToString("G29", CultureInfo.InvariantCulture),
            values.PitPaymentOptionThreshold.ToString("G29", CultureInfo.InvariantCulture),
            values.HealthRatePercent.ToString("G29", CultureInfo.InvariantCulture),
            values.HealthMinimumChangeMonth.ToString(CultureInfo.InvariantCulture),
            values.HealthMinimumBeforeChange.ToString("G29", CultureInfo.InvariantCulture),
            values.HealthMinimumFromChange.ToString("G29", CultureInfo.InvariantCulture),
            officialSources,
            capturedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
