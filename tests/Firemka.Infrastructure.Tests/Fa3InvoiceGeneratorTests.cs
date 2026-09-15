using System.Xml.Linq;
using Firemka.Application.Sales;
using Firemka.Infrastructure.Ksef.Outgoing;

namespace Firemka.Infrastructure.Tests;

public sealed class Fa3InvoiceGeneratorTests
{
    [Fact]
    public void Generated_subscription_invoice_is_valid_fa3_and_keeps_service_period_separate_from_issue_date()
    {
        var generator = new Fa3InvoiceGenerator();

        var xml = generator.Generate(CreateInput());
        generator.Validate(xml);

        var document = XDocument.Parse(xml);
        XNamespace ns = Fa3InvoiceGenerator.Fa3Namespace;
        Assert.Equal("2026-10-02", document.Descendants(ns + "P_1").Single().Value);
        Assert.Equal("2026-09-01", document.Descendants(ns + "P_6_Od").Single().Value);
        Assert.Equal("2026-09-30", document.Descendants(ns + "P_6_Do").Single().Value);
        Assert.Equal("2026-10-09", document.Descendants(ns + "Termin").Single().Value);
        Assert.Equal("1500.00", document.Descendants(ns + "P_13_1").Single().Value);
        Assert.Equal("345.00", document.Descendants(ns + "P_14_1").Single().Value);
        Assert.Equal("1845.00", document.Descendants(ns + "P_15").Single().Value);
    }

    [Fact]
    public void Same_input_generates_byte_identical_xml_for_manual_and_automatic_paths()
    {
        var generator = new Fa3InvoiceGenerator();
        var input = CreateInput();

        Assert.Equal(generator.Generate(input), generator.Generate(input));
    }

    private static Fa3InvoiceInput CreateInput() => new(
        Guid.Parse("7d5032c1-b5f9-4f96-8fb5-47cc93a9ef61"),
        "FV/2026/09",
        DateTimeOffset.Parse("2026-10-02T08:00:00Z"),
        new DateOnly(2026, 10, 2),
        new DateOnly(2026, 9, 1),
        new DateOnly(2026, 9, 30),
        "Testowa Firma",
        "1234563218",
        "Testowa 2, 00-002 Warszawa",
        "Testowy Klient",
        "1234563218",
        "Testowa 1, 00-001 Warszawa",
        "Miesięczny dostęp do aplikacji Test SaaS",
        1_500m,
        23m,
        345m,
        1_845m,
        new DateOnly(2026, 10, 9),
        "PLN");
}
