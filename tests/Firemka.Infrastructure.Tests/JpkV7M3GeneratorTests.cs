using System.Xml.Linq;
using Firemka.Application.Filings;
using Firemka.Infrastructure.Filings.Jpk;

namespace Firemka.Infrastructure.Tests;

public sealed class JpkV7M3GeneratorTests
{
    [Fact]
    public void Vat_surplus_is_carried_forward_without_requesting_a_refund()
    {
        var generator = new JpkV7M3Generator();
        var input = new JpkV7M3Input(
            Identity(),
            new DateOnly(2026, 9, 1),
            DateTimeOffset.Parse("2026-10-02T08:00:00Z"),
            1,
            [],
            [new JpkVatPurchaseRow(
                "DE", "123456789", "Syntetyczny Dostawca", "K/09/2026",
                new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), null, 100m, 23m)],
            50m);

        var xml = generator.Generate(input);
        generator.Validate(xml);

        var document = XDocument.Parse(xml);
        XNamespace ns = JpkV7M3Generator.JpkNamespace;
        Assert.Equal("50", document.Descendants(ns + "P_39").Single().Value);
        Assert.Equal("73", document.Descendants(ns + "P_48").Single().Value);
        Assert.Equal("73", document.Descendants(ns + "P_53").Single().Value);
        Assert.Equal("73", document.Descendants(ns + "P_62").Single().Value);
        Assert.Empty(document.Descendants(ns + "P_54"));
        Assert.Empty(document.Descendants(ns + "P_540"));
        Assert.Equal("DE", document.Descendants(ns + "KodKrajuNadaniaTIN").Single().Value);
    }

    [Fact]
    public void Generated_monthly_vat_file_maps_rows_totals_and_validates_against_exact_xsd()
    {
        var generator = new JpkV7M3Generator();
        var input = new JpkV7M3Input(
            Identity(),
            new DateOnly(2026, 9, 1),
            DateTimeOffset.Parse("2026-10-02T08:00:00Z"),
            1,
            [new JpkVatSalesRow(
                "1234567890",
                "Syntetyczny Klient",
                "FV/09/2026",
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 1),
                "1010000000-20260101-000000000000-00",
                1_000m,
                230m)],
            [new JpkVatPurchaseRow(
                null,
                "BRAK",
                "Syntetyczny Dostawca",
                "K/09/2026",
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 10),
                null,
                100m,
                23m)],
            0m);

        var xml = generator.Generate(input);
        generator.Validate(xml);

        var document = XDocument.Parse(xml);
        XNamespace ns = JpkV7M3Generator.JpkNamespace;
        Assert.Equal("1000.00", document.Descendants(ns + "K_19").Single().Value);
        Assert.Equal("230.00", document.Descendants(ns + "K_20").Single().Value);
        Assert.Equal("100.00", document.Descendants(ns + "K_42").Single().Value);
        Assert.Equal("23.00", document.Descendants(ns + "K_43").Single().Value);
        Assert.Equal("207", document.Descendants(ns + "P_51").Single().Value);
        Assert.Equal("1", document.Descendants(ns + "LiczbaWierszySprzedazy").Single().Value);
        Assert.Equal("1", document.Descendants(ns + "LiczbaWierszyZakupow").Single().Value);
        Assert.Empty(document.Descendants(ns + "ZakupWiersz").Single().Elements(ns + "KodKrajuNadaniaTIN"));
        Assert.Equal("1", document.Descendants(ns + "ZakupWiersz").Single().Element(ns + "BFK")?.Value);
        Assert.Empty(document.Descendants(ns + "OFF"));
    }

    [Fact]
    public void Electronic_or_paper_invoice_without_a_ksef_number_uses_bfk_not_emergency_off()
    {
        var generator = new JpkV7M3Generator();
        var input = new JpkV7M3Input(
            Identity(),
            new DateOnly(2026, 9, 1),
            DateTimeOffset.Parse("2026-10-02T08:00:00Z"),
            1,
            [new JpkVatSalesRow(
                "1234567890", "Syntetyczny Klient", "FV/09/2026",
                new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), null, 1_000m, 230m)],
            [new JpkVatPurchaseRow(
                "PL", "9876543210", "Syntetyczny Dostawca", "K/09/2026",
                new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 10), null, 100m, 23m)],
            0m);

        var xml = generator.Generate(input);
        generator.Validate(xml);

        var document = XDocument.Parse(xml);
        XNamespace ns = JpkV7M3Generator.JpkNamespace;
        Assert.Equal(2, document.Descendants(ns + "BFK").Count());
        Assert.Empty(document.Descendants(ns + "OFF"));
    }

    private static FilingPersonIdentity Identity() => new(
        "1010000000", "Jan", "Testowy", new DateOnly(1990, 1, 1),
        "90010112345", "1215", "0510", "jan@example.test");
}
