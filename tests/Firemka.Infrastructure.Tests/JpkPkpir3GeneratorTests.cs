using System.Xml.Linq;
using Firemka.Application.Filings;
using Firemka.Infrastructure.Filings.Jpk;

namespace Firemka.Infrastructure.Tests;

public sealed class JpkPkpir3GeneratorTests
{
    [Fact]
    public void Generated_pkpir_maps_revenue_costs_and_validates_against_exact_xsd()
    {
        var generator = new JpkPkpir3Generator();
        var xml = generator.Generate(new JpkPkpir3Input(
            Identity(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 9, 30),
            DateTimeOffset.Parse("2026-10-02T08:00:00Z"),
            0,
            [
                new JpkPkpirRow(new DateOnly(2026, 9, 1), "FV/09/2026", null,
                    "PL", "1234567890", "Syntetyczny Klient", "Adres testowy", "Usługa testowa", 1_000m, 0m),
                new JpkPkpirRow(new DateOnly(2026, 9, 10), "K/09/2026", null,
                    null, null, "Syntetyczny Dostawca", "Adres testowy", "Koszt testowy", 0m, 100m),
            ]));

        generator.Validate(xml);
        var document = XDocument.Parse(xml);
        XNamespace ns = JpkPkpir3Generator.JpkNamespace;
        Assert.Equal("1000.00", document.Descendants(ns + "SumaPrzychodow").Single().Value);
        Assert.Equal("100.00", document.Descendants(ns + "P_3").Single().Value);
        Assert.Equal("900.00", document.Descendants(ns + "P_4").Single().Value);
        Assert.Equal(2, document.Descendants(ns + "PKPIRWiersz").Count());
        Assert.Single(document.Descendants(ns + "K_4B"));
    }

    [Fact]
    public void Annual_file_contains_confirmed_inventory_values()
    {
        var generator = new JpkPkpir3Generator();
        var xml = generator.Generate(new JpkPkpir3Input(
            Identity(), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            DateTimeOffset.Parse("2027-01-10T10:00:00Z"), 0,
            [new JpkPkpirRow(new DateOnly(2026, 1, 2), "FV/1", null, "PL", "1234567890",
                "Klient", "Adres", "Usługa", 100m, 0m)], 1200.50m, 900.25m));

        var document = XDocument.Parse(xml);
        XNamespace ns = JpkPkpir3Generator.JpkNamespace;
        Assert.Equal("1200.50", document.Descendants(ns + "P_1").Single().Value);
        Assert.Equal("900.25", document.Descendants(ns + "P_2").Single().Value);
        Assert.Equal("300.25", document.Descendants(ns + "P_3").Single().Value);
        Assert.Equal("-200.25", document.Descendants(ns + "P_4").Single().Value);
        generator.Validate(xml);
    }

    private static FilingPersonIdentity Identity() => new(
        "1010000000", "Jan", "Testowy", new DateOnly(1990, 1, 1),
        "90010112345", "1215", "0510", "jan@example.test");
}
