using System.Globalization;
using Firemka.Domain.Calculations;

namespace Firemka.Domain.Tests;

public sealed class MonthCalculationTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Month = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Reference_rule_needs_a_new_evidenced_revision_before_closing()
    {
        var reference = Rules2026();

        var confirmed = reference.Confirm("opinia księgowej TEST/2026", new DateOnly(2026, 9, 12), Now);

        Assert.Equal(CalculationRuleTrust.ReferenceOnly, reference.Trust);
        Assert.Equal(1, reference.VersionNumber);
        Assert.Null(reference.IndependentEvidenceReference);
        Assert.Equal(2, confirmed.VersionNumber);
        Assert.Equal(reference.Id, confirmed.PreviousRuleSetId);
        Assert.Equal(CalculationRuleTrust.IndependentlyConfirmed, confirmed.Trust);
        Assert.Equal(reference.Values, confirmed.Values);
        Assert.Equal(reference.OfficialSources, confirmed.OfficialSources);
    }

    [Theory]
    [InlineData("100000", "8400")]
    [InlineData("130000", "14000")]
    public void Tax_scale_uses_both_brackets_and_whole_zloty_rounding(
        string income,
        string expectedTax)
    {
        var result = Calculate(
            revenueMonth: decimal.Parse(income, CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(expectedTax, CultureInfo.InvariantCulture), result.CumulativePitTax);
        Assert.Equal(decimal.Parse(income, CultureInfo.InvariantCulture), result.PitIncomeYtd);
    }

    [Fact]
    public void Loss_yields_zero_pit_and_zero_current_advance()
    {
        var result = Calculate(revenueMonth: 1_000m, costsMonth: 2_500m);

        Assert.Equal(-1_500m, result.PitIncomeYtd);
        Assert.Equal(0m, result.PitTaxBase);
        Assert.Equal(0m, result.CumulativePitTax);
        Assert.Equal(0m, result.PitAdvanceDue);
        Assert.False(result.CanDeferPitPayment);
    }

    [Fact]
    public void Previous_advances_reduce_only_the_current_advance()
    {
        var result = Calculate(revenueMonth: 100_000m, priorPitAdvancesDue: 2_000m);

        Assert.Equal(8_400m, result.CumulativePitTax);
        Assert.Equal(2_000m, result.PriorPitAdvancesDue);
        Assert.Equal(6_400m, result.PitAdvanceDue);
    }

    [Fact]
    public void Advance_at_one_thousand_is_returned_and_marked_as_deferrable()
    {
        var result = Calculate(revenueMonth: 38_333m);

        Assert.Equal(1_000m, result.PitAdvanceDue);
        Assert.True(result.CanDeferPitPayment);
    }

    [Fact]
    public void Vat_surplus_carries_and_health_uses_previous_month_with_minimum()
    {
        var result = Calculate(
            outputVat: 100m,
            inputVat: 300m,
            priorVatCarry: 50m,
            previousHealthIncome: 1_000m);

        Assert.Equal(250m, result.VatCarryForward);
        Assert.Equal(0m, result.VatPayable);
        Assert.Equal(0m, result.VatPayableRounded);
        Assert.Equal(4_806m, result.HealthBasis);
        Assert.Equal(432.54m, result.HealthContribution);
    }

    [Fact]
    public void Vat_payable_keeps_cents_and_exposes_a_whole_zloty_estimate()
    {
        var result = Calculate(outputVat: 1_100.75m, inputVat: 100.25m, priorVatCarry: 100m);

        Assert.Equal(900.50m, result.VatPayable);
        Assert.Equal(901m, result.VatPayableRounded);
        Assert.Equal(0m, result.VatCarryForward);
    }

    [Fact]
    public void January_and_february_use_different_minimum_health_bases()
    {
        var rules = Rules2026();

        Assert.Equal(3_499.50m, rules.GetMinimumHealthBase(new DateOnly(2026, 1, 1)));
        Assert.Equal(4_806m, rules.GetMinimumHealthBase(new DateOnly(2026, 2, 1)));
        Assert.Throws<InvalidOperationException>(() =>
            rules.GetMinimumHealthBase(new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Invalid_rule_values_sources_and_confirmation_evidence_are_rejected()
    {
        var valid = RuleValues();

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { PitThreshold = 0m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { PitLowerRatePercent = -1m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { PitHigherRatePercent = 101m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { PitReducingAmount = -1m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { PitPaymentOptionThreshold = -1m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { HealthRatePercent = 101m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { HealthMinimumChangeMonth = 13 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { HealthMinimumBeforeChange = 0m }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(valid with { HealthMinimumFromChange = 0m }));
        Assert.Throws<ArgumentException>(() => CalculationRuleSet.CreateReference(
            CompanyId,
            "owner-1",
            2026,
            valid,
            " ",
            new DateOnly(2026, 9, 12),
            Now));
        Assert.Throws<ArgumentException>(() => Rules2026().Confirm(" ", Month, Now));
    }

    [Fact]
    public void Calculation_rejects_mismatched_or_untrusted_inputs()
    {
        var confirmed = ConfirmedRules2026();
        var valid = Input(confirmed);

        Assert.Throws<InvalidOperationException>(() => MonthCalculation.Calculate(
            valid with { CompanyId = Guid.NewGuid() }, confirmed, 1, null, Now));
        Assert.Throws<InvalidOperationException>(() => MonthCalculation.Calculate(
            valid with { RuleSetId = Guid.NewGuid() }, confirmed, 1, null, Now));
        Assert.Throws<ArgumentException>(() => MonthCalculation.Calculate(
            valid with { Month = new DateOnly(2026, 9, 2) }, confirmed, 1, null, Now));
        Assert.Throws<ArgumentException>(() => MonthCalculation.Calculate(
            valid with { InputFingerprint = "short" }, confirmed, 1, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthCalculation.Calculate(
            valid with { RevenueMonth = -1m }, confirmed, 1, null, Now));
        Assert.Throws<ArgumentException>(() => MonthCalculation.Calculate(
            valid with
            {
                SourceLines = [new CalculationSourceLine("SALE", "Sprzedaż", 1m, " ")],
            },
            confirmed,
            1,
            null,
            Now));

        var reference = Rules2026();
        Assert.Throws<InvalidOperationException>(() => MonthCalculation.Calculate(
            Input(reference) with { Trust = CalculationTrust.ClosingEligible },
            reference,
            1,
            null,
            Now));
    }

    [Fact]
    public void Source_and_formula_lines_have_a_stable_sequence()
    {
        var result = Calculate();

        Assert.Equal(
            [
                "SALE:TEST",
                "PIT_REVENUE_YTD",
                "PIT_COSTS_YTD",
                "PIT_INCOME_YTD",
                "PIT_TAX_BASE",
                "PIT_CUMULATIVE_TAX",
                "PIT_ADVANCE_DUE",
                "VAT_PAYABLE",
                "VAT_CARRY_FORWARD",
                "HEALTH_CURRENT_INCOME",
                "HEALTH_BASIS",
                "HEALTH_CONTRIBUTION",
            ],
            result.Lines.OrderBy(item => item.Sequence).Select(item => item.Code));
        Assert.False(result.Lines.First().IsFormula);
        Assert.All(result.Lines.Skip(1), item => Assert.True(item.IsFormula));
    }

    private static MonthCalculation Calculate(
        decimal revenueMonth = 0m,
        decimal costsMonth = 0m,
        decimal priorPitAdvancesDue = 0m,
        decimal priorVatCarry = 0m,
        decimal previousHealthIncome = 0m,
        decimal outputVat = 0m,
        decimal inputVat = 0m)
    {
        var rules = ConfirmedRules2026();
        var input = Input(rules) with
        {
            RevenueMonth = revenueMonth,
            CostsMonth = costsMonth,
            PriorPitAdvancesDue = priorPitAdvancesDue,
            PriorVatCarryForward = priorVatCarry,
            PreviousMonthHealthIncome = previousHealthIncome,
            OutputVatMonth = outputVat,
            InputVatMonth = inputVat,
        };

        return MonthCalculation.Calculate(input, rules, 1, null, Now);
    }

    private static MonthCalculationInput Input(CalculationRuleSet rules)
        => new(
            CompanyId,
            "owner-1",
            Month,
            rules.Id,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            null,
            Fingerprint,
            RevenueMonth: 0m,
            CostsMonth: 0m,
            SocialContributionsMonth: 0m,
            PitBaseAdjustmentMonth: 0m,
            HealthIncomeAdjustmentMonth: 0m,
            RevenueYtdBefore: 0m,
            CostsYtdBefore: 0m,
            SocialContributionsYtdBefore: 0m,
            PitAdjustmentsYtdBefore: 0m,
            PriorPitAdvancesDue: 0m,
            PriorVatCarryForward: 0m,
            PreviousMonthHealthIncome: 0m,
            OutputVatMonth: 0m,
            InputVatMonth: 0m,
            CalculationTrust.ClosingEligible,
            [new CalculationSourceLine("SALE:TEST", "Sprzedaż testowa", 0m, "synthetic:invoice")]);

    private static CalculationRuleSet ConfirmedRules2026()
        => Rules2026().Confirm("opinia księgowej TEST/2026", new DateOnly(2026, 9, 12), Now);

    private static CalculationRuleSet Rules2026()
        => CreateRule(RuleValues());

    private static CalculationRuleSet CreateRule(CalculationRuleValues values)
        => CalculationRuleSet.CreateReference(
            CompanyId,
            "owner-1",
            2026,
            values,
            "https://example.test/official-source",
            new DateOnly(2026, 9, 12),
            Now);

    private static CalculationRuleValues RuleValues()
        => new(
            PitThreshold: 120_000m,
            PitLowerRatePercent: 12m,
            PitHigherRatePercent: 32m,
            PitReducingAmount: 3_600m,
            PitPaymentOptionThreshold: 1_000m,
            HealthRatePercent: 9m,
            HealthMinimumChangeMonth: 2,
            HealthMinimumBeforeChange: 3_499.50m,
            HealthMinimumFromChange: 4_806m);
}
