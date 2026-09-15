using System.Xml.Linq;
using Firemka.Application.Filings;
using Firemka.Infrastructure.Filings.Zus;

namespace Firemka.Infrastructure.Tests;

public sealed class ZusDraKedu227GeneratorTests
{
    [Fact]
    public void Generated_health_only_dra_maps_period_and_amounts_and_validates_against_kedu_xsd()
    {
        var generator = new ZusDraKedu227Generator();
        var xml = generator.Generate(new ZusDraKedu227Input(
            new FilingPersonIdentity(
                "1010000000", "Jan", "Testowy", new DateOnly(1990, 1, 1),
                "90010112345", "1215", "0510", "jan@example.test"),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 2),
            2,
            5_000m,
            450m));

        generator.Validate(xml);
        var document = XDocument.Parse(xml);
        XNamespace ns = ZusDraKedu227Generator.KeduNamespace;
        var sectionX = document.Descendants(ns + "X").Single();
        var organization = document.Descendants(ns + "I").Single();
        Assert.Equal("6", organization.Elements(ns + "p1").Single().Value);
        Assert.Equal("02", organization.Element(ns + "p2")!.Element(ns + "p1")!.Value);
        Assert.Equal("2026-09", organization.Element(ns + "p2")!.Element(ns + "p2")!.Value);
        Assert.Equal("5000.00", sectionX.Elements(ns + "p2").Single().Value);
        Assert.Equal("450.00", sectionX.Elements(ns + "p3").Single().Value);
    }
}
