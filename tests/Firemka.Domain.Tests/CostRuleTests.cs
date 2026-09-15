using Firemka.Domain.Accounting;

namespace Firemka.Domain.Tests;

public sealed class CostRuleTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public void Amount_and_date_are_not_part_of_the_fingerprint_but_every_tax_field_is()
    {
        var fingerprint = Fingerprint("pl 123-456", "pl", "pln", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai);
        var rule = CreateRule(fingerprint);

        Assert.True(rule.Matches(Fingerprint("PL123456", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai)));
        Assert.False(rule.Matches(Fingerprint("PL999", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai)));
        Assert.False(rule.Matches(Fingerprint("PL123456", "US", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai)));
        Assert.False(rule.Matches(Fingerprint("PL123456", "PL", "EUR", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai)));
        Assert.False(rule.Matches(Fingerprint("PL123456", "PL", "PLN", VatTreatment.ForeignService, null, CostServiceKind.Ai)));
        Assert.False(rule.Matches(Fingerprint("PL123456", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Vps)));
    }

    [Fact]
    public void Revision_keeps_history_and_manual_period_policy_disables_automatic_booking()
    {
        var first = CreateRule(Fingerprint("PL123", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai));

        var second = first.CreateRevision(
            "Pozostałe wydatki",
            50m,
            75m,
            AccountingPeriodPolicy.Manual,
            AccountingPeriodPolicy.IssueMonth,
            "potwierdzona notatka",
            Now.AddDays(1));

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.Id, second.PreviousRuleId);
        Assert.False(second.CanBookAutomatically);
        Assert.True(first.IsActive);
    }

    [Fact]
    public void Rule_resolves_issue_and_next_month_but_requires_manual_month_when_configured()
    {
        var issueDate = new DateOnly(2026, 12, 17);
        var rule = CostRule.CreateInitial(
            CompanyId,
            "owner-1",
            Fingerprint("PL123", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Internet),
            "Pozostałe wydatki",
            100m,
            100m,
            AccountingPeriodPolicy.IssueMonth,
            AccountingPeriodPolicy.NextMonth,
            "decyzja testowa",
            Now);

        Assert.Equal(new DateOnly(2026, 12, 1), rule.ResolveKpirPeriod(issueDate));
        Assert.Equal(new DateOnly(2027, 1, 1), rule.ResolveVatPeriod(issueDate));

        var manual = rule.CreateRevision("Pozostałe wydatki", 100m, 100m, AccountingPeriodPolicy.Manual, AccountingPeriodPolicy.Manual, "ręcznie", Now.AddDays(1));
        Assert.Throws<InvalidOperationException>(() => manual.ResolveKpirPeriod(issueDate));
        Assert.Throws<InvalidOperationException>(() => manual.ResolveVatPeriod(issueDate));
    }

    [Fact]
    public void Invalid_percentages_and_incomplete_domestic_vat_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Fingerprint("PL123", "PL", "PLN", VatTreatment.DomesticTaxed, null, CostServiceKind.Ai));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRule(
            Fingerprint("PL123", "PL", "PLN", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai),
            vatPercent: 101m));
    }

    private static CostRule CreateRule(CostDocumentFingerprint fingerprint, decimal vatPercent = 50m)
        => CostRule.CreateInitial(
            CompanyId,
            "owner-1",
            fingerprint,
            "Pozostałe wydatki",
            vatPercent,
            75m,
            AccountingPeriodPolicy.IssueMonth,
            AccountingPeriodPolicy.IssueMonth,
            "potwierdzona decyzja",
            Now);

    private static CostDocumentFingerprint Fingerprint(
        string seller,
        string country,
        string currency,
        VatTreatment vatTreatment,
        decimal? vatRate,
        CostServiceKind kind)
        => CostDocumentFingerprint.Create(seller, country, currency, vatTreatment, vatRate, kind);
}
