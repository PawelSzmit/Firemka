using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Firemka.Application.Filings;

namespace Firemka.Infrastructure.Filings.Jpk;

public sealed class JpkV7M3Generator : IJpkV7M3Generator
{
    public const string JpkNamespace = "http://crd.gov.pl/wzor/2025/12/19/14090/";
    private const string EtdNamespace = "http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/09/13/eD/DefinicjeTypy/";
    private static readonly XNamespace Ns = JpkNamespace;
    private static readonly XNamespace Etd = EtdNamespace;

    public string Generate(JpkV7M3Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        var outputNet = input.Sales.Sum(item => item.NetAmount23);
        var outputVat = input.Sales.Sum(item => item.VatAmount23);
        var inputVat = input.Purchases.Sum(item => item.DeductibleVatAmount);
        var difference = outputVat - inputVat - input.PriorVatCarryForward;
        var payable = decimal.Max(0m, difference);
        var carry = decimal.Max(0m, -difference);

        var details = new XElement(Ns + "PozycjeSzczegolowe",
            WholeElement("P_37", outputNet),
            WholeElement("P_38", outputVat),
            WholeElement("P_39", input.PriorVatCarryForward),
            WholeElement("P_40", 0),
            WholeElement("P_41", 0),
            WholeElement("P_42", 0),
            WholeElement("P_43", 0),
            WholeElement("P_44", 0),
            WholeElement("P_45", 0),
            WholeElement("P_46", 0),
            WholeElement("P_47", 0),
            WholeElement("P_48", inputVat + input.PriorVatCarryForward),
            WholeElement("P_51", payable),
            WholeElement("P_53", carry),
            WholeElement("P_62", carry));

        var evidence = new XElement(Ns + "Ewidencja");
        for (var index = 0; index < input.Sales.Count; index++)
        {
            var row = input.Sales[index];
            evidence.Add(new XElement(Ns + "SprzedazWiersz",
                new XElement(Ns + "LpSprzedazy", index + 1),
                new XElement(Ns + "KodKrajuNadaniaTIN", "PL"),
                new XElement(Ns + "NrKontrahenta", row.CounterpartyTaxId),
                new XElement(Ns + "NazwaKontrahenta", row.CounterpartyName),
                new XElement(Ns + "DowodSprzedazy", row.EvidenceNumber),
                new XElement(Ns + "DataWystawienia", Date(row.IssueDate)),
                new XElement(Ns + "DataSprzedazy", Date(row.SaleDate)),
                InvoicePresence(row.KsefNumber),
                new XElement(Ns + "K_19", Money(row.NetAmount23)),
                new XElement(Ns + "K_20", Money(row.VatAmount23))));
        }

        evidence.Add(new XElement(Ns + "SprzedazCtrl",
            new XElement(Ns + "LiczbaWierszySprzedazy", input.Sales.Count),
            new XElement(Ns + "PodatekNalezny", Money(outputVat))));
        for (var index = 0; index < input.Purchases.Count; index++)
        {
            var row = input.Purchases[index];
            evidence.Add(new XElement(Ns + "ZakupWiersz",
                new XElement(Ns + "LpZakupu", index + 1),
                string.Equals(row.SupplierTaxId, "BRAK", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(row.SupplierCountryCode)
                    ? null
                    : new XElement(Ns + "KodKrajuNadaniaTIN", row.SupplierCountryCode),
                new XElement(Ns + "NrDostawcy", row.SupplierTaxId),
                new XElement(Ns + "NazwaDostawcy", row.SupplierName),
                new XElement(Ns + "DowodZakupu", row.EvidenceNumber),
                new XElement(Ns + "DataZakupu", Date(row.PurchaseDate)),
                new XElement(Ns + "DataWplywu", Date(row.ReceivedDate)),
                InvoicePresence(row.KsefNumber),
                new XElement(Ns + "K_42", Money(row.NetOtherAmount)),
                new XElement(Ns + "K_43", Money(row.DeductibleVatAmount))));
        }

        evidence.Add(new XElement(Ns + "ZakupCtrl",
            new XElement(Ns + "LiczbaWierszyZakupow", input.Purchases.Count),
            new XElement(Ns + "PodatekNaliczony", Money(inputVat))));

        var person = new XElement(Ns + "OsobaFizyczna",
            new XElement(Etd + "NIP", input.Identity.Nip),
            new XElement(Etd + "ImiePierwsze", input.Identity.FirstName),
            new XElement(Etd + "Nazwisko", input.Identity.LastName),
            new XElement(Etd + "DataUrodzenia", Date(input.Identity.BirthDate)));
        if (!string.IsNullOrWhiteSpace(input.Identity.Email))
        {
            person.Add(new XElement(Ns + "Email", input.Identity.Email));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Ns + "JPK",
                new XAttribute(XNamespace.Xmlns + "etd", EtdNamespace),
                new XElement(Ns + "Naglowek",
                    new XElement(Ns + "KodFormularza",
                        new XAttribute("kodSystemowy", "JPK_V7M (3)"),
                        new XAttribute("wersjaSchemy", "1-0E"),
                        "JPK_VAT"),
                    new XElement(Ns + "WariantFormularza", "3"),
                    new XElement(Ns + "DataWytworzeniaJPK", XmlConvert.ToString(input.GeneratedAtUtc.UtcDateTime, XmlDateTimeSerializationMode.Utc)),
                    new XElement(Ns + "NazwaSystemu", "Firemka"),
                    new XElement(Ns + "CelZlozenia", new XAttribute("poz", "P_7"), input.SubmissionPurpose),
                    new XElement(Ns + "KodUrzedu", input.Identity.TaxOfficeCode),
                    new XElement(Ns + "Rok", input.Month.Year),
                    new XElement(Ns + "Miesiac", input.Month.Month)),
                new XElement(Ns + "Podmiot1", new XAttribute("rola", "Podatnik"), person),
                new XElement(Ns + "Deklaracja",
                    new XElement(Ns + "Naglowek",
                        new XElement(Ns + "KodFormularzaDekl",
                            new XAttribute("kodSystemowy", "VAT-7 (23)"),
                            new XAttribute("kodPodatku", "VAT"),
                            new XAttribute("rodzajZobowiazania", "Z"),
                            new XAttribute("wersjaSchemy", "1-0E"),
                            "VAT-7"),
                        new XElement(Ns + "WariantFormularzaDekl", "23")),
                    details,
                    new XElement(Ns + "Pouczenia", "1")),
                evidence));

        return Utf8Xml.Serialize(document);
    }

    public void Validate(string xml)
        => FilingSchemaValidator.Validate(xml, "jpk-v7m3-2026-09-09.xsd", "JPK_V7M(3)");

    private static void ValidateInput(JpkV7M3Input input)
    {
        ArgumentNullException.ThrowIfNull(input.Identity);
        if (input.Month.Day != 1 || input.SubmissionPurpose is < 1 or > 2
            || input.PriorVatCarryForward < 0
            || input.Sales.Any(row => row.NetAmount23 < 0 || row.VatAmount23 < 0)
            || input.Purchases.Any(row => row.NetOtherAmount < 0 || row.DeductibleVatAmount < 0))
        {
            throw new ArgumentException("Dane JPK_V7M są nieprawidłowe.", nameof(input));
        }
    }

    private static XElement WholeElement(string name, decimal amount)
        => new(Ns + name, decimal.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture));
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static object? Optional(XName name, string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new XElement(name, value);

    private static XElement InvoicePresence(string? ksefNumber)
        => string.IsNullOrWhiteSpace(ksefNumber)
            ? new XElement(Ns + "BFK", "1")
            : new XElement(Ns + "NrKSeF", ksefNumber);
}
