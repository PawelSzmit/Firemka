using Firemka.Domain.Calculations;

namespace Firemka.Infrastructure.Calculations;

public static class CalculationReferenceCatalog
{
    private const string OfficialSources = """
        https://www.podatki.gov.pl/podatki-firmowe/pit/informacje-podstawowe/co-jest-opodatkowane/opodatkowanie-wedlug-skali-podatkowej
        https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej
        https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/odliczenie-i-zwrot-podatku-vat
        https://eli.gov.pl/api/acts/DU/2022/2651/text.html
        """;

    public static CalculationRuleSet Create2026(
        Guid companyId,
        string ownerUserId,
        DateTimeOffset nowUtc)
        => Create(companyId, ownerUserId, 2026, nowUtc);

    public static CalculationRuleSet Create(
        Guid companyId,
        string ownerUserId,
        int taxYear,
        DateTimeOffset nowUtc)
    {
        if (taxYear != 2026)
        {
            throw new InvalidOperationException(
                $"Brakuje sprawdzonego katalogu zasad dla roku {taxYear}; wartości z 2026 nie zostaną skopiowane.");
        }

        return CalculationRuleSet.CreateReference(
            companyId,
            ownerUserId,
            taxYear,
            new CalculationRuleValues(
                PitThreshold: 120_000m,
                PitLowerRatePercent: 12m,
                PitHigherRatePercent: 32m,
                PitReducingAmount: 3_600m,
                PitPaymentOptionThreshold: 1_000m,
                HealthRatePercent: 9m,
                HealthMinimumChangeMonth: 2,
                HealthMinimumBeforeChange: 3_499.50m,
                HealthMinimumFromChange: 4_806m),
            OfficialSources,
            new DateOnly(2026, 9, 12),
            nowUtc);
    }
}
