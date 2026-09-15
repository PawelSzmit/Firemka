using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Firemka.Application.Filings;

namespace Firemka.Infrastructure.Filings.Zus;

public sealed class ZusDraKedu227Generator : IZusDraKedu227Generator
{
    public const string KeduNamespace = "http://www.zus.pl/2026/KEDU_5_7";
    private static readonly XNamespace Ns = KeduNamespace;

    public string Generate(ZusDraKedu227Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Month.Day != 1 || input.FilingSequenceNumber is < 1 or > 99
            || input.HealthBasis < 0 || input.HealthContribution < 0)
        {
            throw new ArgumentException("Dane ZUS DRA są nieprawidłowe.", nameof(input));
        }

        XElement Zeros(string section, int count, params int[] omitted) =>
            new(Ns + section, Enumerable.Range(1, count)
                .Where(number => !omitted.Contains(number))
                .Select(number => new XElement(Ns + $"p{number}", "0.00")));
        var dra = new XElement(Ns + "ZUSDRA",
            new XAttribute("id_dokumentu", "1"),
            new XAttribute("status_kontroli", "0"),
            new XAttribute("status_weryfikacji", "P"),
            new XElement(Ns + "identyfikacja.PL", new XElement(Ns + "id_PL_ZUS_status", "B")),
            new XElement(Ns + "I",
                new XElement(Ns + "p1", "6"),
                new XElement(Ns + "p2",
                    new XElement(Ns + "p1", input.FilingSequenceNumber.ToString("00", CultureInfo.InvariantCulture)),
                    new XElement(Ns + "p2", input.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture)))),
            new XElement(Ns + "II",
                new XElement(Ns + "p1", input.Identity.Nip),
                new XElement(Ns + "p3", input.Identity.Pesel),
                new XElement(Ns + "p7", input.Identity.LastName),
                new XElement(Ns + "p8", input.Identity.FirstName),
                new XElement(Ns + "p9", input.Identity.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
            new XElement(Ns + "III", new XElement(Ns + "p1", "1"), new XElement(Ns + "p3", "0.00")),
            Zeros("IV", 37),
            Zeros("V", 5),
            Zeros("VI", 7),
            Zeros("VII", 3, 2),
            Zeros("IX", 2),
            new XElement(Ns + "X",
                new XElement(Ns + "p1",
                    new XElement(Ns + "p1", input.Identity.ZusInsuranceTitleCode),
                    new XElement(Ns + "p2", "0"),
                    new XElement(Ns + "p3", "0")),
                new XElement(Ns + "p2", Money(input.HealthBasis)),
                new XElement(Ns + "p3", Money(input.HealthContribution)),
                new XElement(Ns + "p4", "0.00"),
                new XElement(Ns + "p5", "0.00")),
            new XElement(Ns + "XIII", new XElement(Ns + "p1", input.PreparedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        var root = new XElement(Ns + "KEDU",
            new XAttribute("wersja_schematu", "1"),
            new XElement(Ns + "naglowek.KEDU",
                new XElement(Ns + "program",
                    new XElement(Ns + "producent", "Firemka"),
                    new XElement(Ns + "symbol", "Firemka"),
                    new XElement(Ns + "wersja", "1.0")),
                new XElement(Ns + "data_utworzenia_KEDU", input.PreparedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(Ns + "status_kontroli", "0")),
            new XElement(Ns + "cechy.KEDU",
                new XElement(Ns + "cecha", new XAttribute("nazwa", "WersjaMetrykiSlownikow"), new XElement(Ns + "wartosc", "152"))),
            dra);
        return Serialize(root);
    }

    public void Validate(string xml)
        => FilingSchemaValidator.Validate(xml, "kedu-2.27-2026-09-09.xsd", "ZUS DRA KEDU 2.27");

    private static string Serialize(XElement root)
    {
        return Utf8Xml.Serialize(new XDocument(new XDeclaration("1.0", "utf-8", null), root));
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
