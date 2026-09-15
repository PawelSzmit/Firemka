using Firemka.Domain.Calculations;
using Firemka.Infrastructure.Calculations;

namespace Firemka.Infrastructure.Tests;

public sealed class CalculationReferenceCatalogTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public void Creates_the_exact_reference_only_2026_rule_from_official_https_sources()
    {
        var rule = CalculationReferenceCatalog.Create2026(CompanyId, "owner-1", Now);

        Assert.Equal(2026, rule.TaxYear);
        Assert.Equal(1, rule.VersionNumber);
        Assert.Equal(CalculationRuleTrust.ReferenceOnly, rule.Trust);
        Assert.Equal(new DateOnly(2026, 9, 12), rule.CapturedOn);
        Assert.Equal(120_000m, rule.Values.PitThreshold);
        Assert.Equal(12m, rule.Values.PitLowerRatePercent);
        Assert.Equal(32m, rule.Values.PitHigherRatePercent);
        Assert.Equal(3_600m, rule.Values.PitReducingAmount);
        Assert.Equal(1_000m, rule.Values.PitPaymentOptionThreshold);
        Assert.Equal(9m, rule.Values.HealthRatePercent);
        Assert.Equal(2, rule.Values.HealthMinimumChangeMonth);
        Assert.Equal(3_499.50m, rule.Values.HealthMinimumBeforeChange);
        Assert.Equal(4_806m, rule.Values.HealthMinimumFromChange);
        Assert.All(
            rule.OfficialSources.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            source => Assert.StartsWith("https://", source, StringComparison.Ordinal));
    }

    [Fact]
    public void Another_year_is_rejected_instead_of_copying_2026_values()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            CalculationReferenceCatalog.Create(CompanyId, "owner-1", 2027, Now));

        Assert.Contains("2027", error.Message, StringComparison.Ordinal);
    }
}
