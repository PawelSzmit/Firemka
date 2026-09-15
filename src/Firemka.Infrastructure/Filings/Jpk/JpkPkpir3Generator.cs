using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Firemka.Application.Filings;

namespace Firemka.Infrastructure.Filings.Jpk;

public sealed class JpkPkpir3Generator : IJpkPkpir3Generator
{
    public const string JpkNamespace = "http://jpk.mf.gov.pl/wzor/2024/10/30/10302/";
    private const string EtdNamespace = "http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/";
    private static readonly XNamespace Ns = JpkNamespace;
    private static readonly XNamespace Etd = EtdNamespace;

    public string Generate(JpkPkpir3Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.PeriodFrom > input.PeriodTo || input.SubmissionPurpose is < 0 or > 1)
        {
            throw new ArgumentException("Okres lub cel JPK_PKPIR jest nieprawidłowy.", nameof(input));
        }

        var revenue = input.Rows.Sum(item => item.Revenue);
        var costs = input.Rows.Sum(item => item.OtherExpense) + input.OpeningInventory - input.ClosingInventory;
        var root = new XElement(Ns + "JPK",
            new XAttribute(XNamespace.Xmlns + "etd", EtdNamespace),
            new XElement(Ns + "Naglowek",
                new XElement(Ns + "KodFormularza",
                    new XAttribute("kodSystemowy", "JPK_PKPIR (3)"),
                    new XAttribute("wersjaSchemy", "1-0"), "JPK_PKPIR"),
                new XElement(Ns + "WariantFormularza", "3"),
                new XElement(Ns + "CelZlozenia", input.SubmissionPurpose),
                new XElement(Ns + "DataWytworzeniaJPK", XmlConvert.ToString(input.GeneratedAtUtc.UtcDateTime, XmlDateTimeSerializationMode.Utc)),
                new XElement(Ns + "DataOd", Date(input.PeriodFrom)),
                new XElement(Ns + "DataDo", Date(input.PeriodTo)),
                new XElement(Ns + "KodUrzedu", input.Identity.TaxOfficeCode)),
            new XElement(Ns + "Podmiot1", new XAttribute("rola", "Podatnik"),
                new XElement(Ns + "OsobaFizyczna",
                    new XElement(Etd + "NIP", input.Identity.Nip),
                    new XElement(Etd + "ImiePierwsze", input.Identity.FirstName),
                    new XElement(Etd + "Nazwisko", input.Identity.LastName),
                    new XElement(Etd + "DataUrodzenia", Date(input.Identity.BirthDate)),
                    string.IsNullOrWhiteSpace(input.Identity.Email) ? null : new XElement(Ns + "Email", input.Identity.Email))),
            new XElement(Ns + "PKPIRInfo",
                new XElement(Ns + "P_1", Money(input.OpeningInventory)),
                new XElement(Ns + "P_2", Money(input.ClosingInventory)),
                new XElement(Ns + "P_3", Money(costs)),
                new XElement(Ns + "P_4", Money(revenue - costs))));

        for (var index = 0; index < input.Rows.Count; index++)
        {
            var row = input.Rows[index];
            root.Add(new XElement(Ns + "PKPIRWiersz",
                new XElement(Ns + "K_1", index + 1),
                new XElement(Ns + "K_2", Date(row.EventDate)),
                new XElement(Ns + "K_3A", row.EvidenceNumber),
                string.IsNullOrWhiteSpace(row.KsefNumber) ? null : new XElement(Ns + "K_3B", row.KsefNumber),
                string.IsNullOrWhiteSpace(row.CounterpartyTaxId) || string.IsNullOrWhiteSpace(row.CounterpartyCountryCode)
                    ? null
                    : new XElement(Ns + "K_4A", row.CounterpartyCountryCode),
                string.IsNullOrWhiteSpace(row.CounterpartyTaxId) ? null : new XElement(Ns + "K_4B", row.CounterpartyTaxId),
                new XElement(Ns + "K_5A", row.CounterpartyName),
                new XElement(Ns + "K_5B", row.CounterpartyAddress),
                new XElement(Ns + "K_6", row.Description),
                row.Revenue == 0 ? null : new XElement(Ns + "K_7", Money(row.Revenue)),
                row.Revenue == 0 ? null : new XElement(Ns + "K_9", Money(row.Revenue)),
                row.OtherExpense == 0 ? null : new XElement(Ns + "K_13", Money(row.OtherExpense)),
                row.OtherExpense == 0 ? null : new XElement(Ns + "K_14", Money(row.OtherExpense))));
        }

        root.Add(new XElement(Ns + "PKPIRCtrl",
            new XElement(Ns + "LiczbaWierszy", input.Rows.Count),
            new XElement(Ns + "SumaPrzychodow", Money(revenue))));
        return Serialize(root);
    }

    public void Validate(string xml)
        => FilingSchemaValidator.Validate(xml, "jpk-pkpir3-2026-09-09.xsd", "JPK_PKPIR(3)");

    private static string Serialize(XElement root)
    {
        return Utf8Xml.Serialize(new XDocument(new XDeclaration("1.0", "utf-8", null), root));
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
