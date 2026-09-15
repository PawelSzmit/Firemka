using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Firemka.Application.Sales;

namespace Firemka.Infrastructure.Ksef.Outgoing;

public sealed class Fa3InvoiceGenerator : IFa3InvoiceGenerator
{
    public const string Fa3Namespace = "http://crd.gov.pl/wzor/2025/06/25/13775/";

    private static readonly XNamespace Namespace = Fa3Namespace;
    private static readonly Lazy<XmlSchemaSet> Schemas = new(CreateSchemas);

    public string Generate(Fa3InvoiceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                Namespace + "Faktura",
                new XElement(
                    Namespace + "Naglowek",
                    new XElement(
                        Namespace + "KodFormularza",
                        new XAttribute("kodSystemowy", "FA (3)"),
                        new XAttribute("wersjaSchemy", "1-0E"),
                        "FA"),
                    new XElement(Namespace + "WariantFormularza", "3"),
                    new XElement(
                        Namespace + "DataWytworzeniaFa",
                        XmlConvert.ToString(input.GeneratedAtUtc.UtcDateTime, XmlDateTimeSerializationMode.Utc)),
                    new XElement(Namespace + "SystemInfo", "Firemka")),
                Party("Podmiot1", input.SellerNip, input.SellerName, input.SellerAddress, includeFlags: false),
                Party("Podmiot2", input.BuyerNip, input.BuyerName, input.BuyerAddress, includeFlags: true),
                new XElement(
                    Namespace + "Fa",
                    new XElement(Namespace + "KodWaluty", input.Currency),
                    new XElement(Namespace + "P_1", Date(input.IssueDate)),
                    new XElement(Namespace + "P_2", input.InvoiceNumber),
                    new XElement(
                        Namespace + "OkresFa",
                        new XElement(Namespace + "P_6_Od", Date(input.ServicePeriodFrom)),
                        new XElement(Namespace + "P_6_Do", Date(input.ServicePeriodTo))),
                    new XElement(Namespace + "P_13_1", Amount(input.NetAmount)),
                    new XElement(Namespace + "P_14_1", Amount(input.VatAmount)),
                    new XElement(Namespace + "P_15", Amount(input.GrossAmount)),
                    Annotations(),
                    new XElement(Namespace + "RodzajFaktury", "VAT"),
                    new XElement(
                        Namespace + "FaWiersz",
                        new XElement(Namespace + "NrWierszaFa", "1"),
                        new XElement(Namespace + "UU_ID", input.InvoiceId.ToString("D")),
                        new XElement(Namespace + "P_7", input.Description),
                        new XElement(Namespace + "P_8A", "mies."),
                        new XElement(Namespace + "P_8B", "1"),
                        new XElement(Namespace + "P_9A", Amount(input.NetAmount)),
                        new XElement(Namespace + "P_11", Amount(input.NetAmount)),
                        new XElement(Namespace + "P_12", Rate(input.VatRate))),
                    new XElement(
                        Namespace + "Platnosc",
                        new XElement(
                            Namespace + "TerminPlatnosci",
                            new XElement(Namespace + "Termin", Date(input.PaymentDueDate))),
                        new XElement(Namespace + "FormaPlatnosci", "6")))));

        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = false,
        }))
        {
            document.Save(writer);
        }

        return builder.ToString();
    }

    public void Validate(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            Schemas = Schemas.Value,
            ValidationType = ValidationType.Schema,
            XmlResolver = null,
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);

        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, settings);
        while (reader.Read())
        {
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Dokument nie jest zgodny z FA(3): {string.Join("; ", errors)}");
        }
    }

    private static XElement Party(
        string elementName,
        string nip,
        string name,
        string address,
        bool includeFlags)
    {
        var party = new XElement(
            Namespace + elementName,
            new XElement(
                Namespace + "DaneIdentyfikacyjne",
                new XElement(Namespace + "NIP", nip),
                new XElement(Namespace + "Nazwa", name)),
            new XElement(
                Namespace + "Adres",
                new XElement(Namespace + "KodKraju", "PL"),
                new XElement(Namespace + "AdresL1", address)));
        if (includeFlags)
        {
            party.Add(
                new XElement(Namespace + "JST", "2"),
                new XElement(Namespace + "GV", "2"));
        }

        return party;
    }

    private static XElement Annotations() => new(
        Namespace + "Adnotacje",
        new XElement(Namespace + "P_16", "2"),
        new XElement(Namespace + "P_17", "2"),
        new XElement(Namespace + "P_18", "2"),
        new XElement(Namespace + "P_18A", "2"),
        new XElement(
            Namespace + "Zwolnienie",
            new XElement(Namespace + "P_19N", "1")),
        new XElement(
            Namespace + "NoweSrodkiTransportu",
            new XElement(Namespace + "P_22N", "1")),
        new XElement(Namespace + "P_23", "2"),
        new XElement(
            Namespace + "PMarzy",
            new XElement(Namespace + "P_PMarzyN", "1")));

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Rate(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void ValidateInput(Fa3InvoiceInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.InvoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SellerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SellerNip);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SellerAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.BuyerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.BuyerNip);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.BuyerAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Currency);
        if (input.InvoiceId == Guid.Empty || input.NetAmount <= 0 || input.GrossAmount <= 0)
        {
            throw new ArgumentException("Dane faktury są niepełne.", nameof(input));
        }

        if (input.PaymentDueDate != input.IssueDate.AddDays(7))
        {
            throw new ArgumentException("Termin płatności musi przypadać siedem dni po wystawieniu.", nameof(input));
        }
    }

    private static XmlSchemaSet CreateSchemas()
    {
        var assembly = typeof(Fa3InvoiceGenerator).Assembly;
        var resolver = new EmbeddedSchemaResolver(assembly);
        var schemaSet = new XmlSchemaSet { XmlResolver = resolver };
        using var schemaStream = resolver.OpenByFileName("fa3-2026-09-09.xsd");
        using var reader = XmlReader.Create(
            schemaStream,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = resolver },
            "embedded:///fa3-2026-09-09.xsd");
        schemaSet.Add(null, reader);
        schemaSet.Compile();
        return schemaSet;
    }

    private sealed class EmbeddedSchemaResolver(Assembly assembly) : XmlResolver
    {
        private readonly IReadOnlyDictionary<string, string> _resources = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith("Firemka.Ksef.Schemas.", StringComparison.Ordinal)
                && name.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                name => ResourceFileName(name),
                name => name,
                StringComparer.OrdinalIgnoreCase);

        public override ICredentials? Credentials
        {
            set { }
        }

        public Stream OpenByFileName(string fileName)
        {
            if (!_resources.TryGetValue(fileName, out var resourceName))
            {
                throw new FileNotFoundException($"Brakuje osadzonego schematu {fileName}.");
            }

            return assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Nie można otworzyć schematu {fileName}.");
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
            => OpenByFileName(Path.GetFileName(absoluteUri.LocalPath));

        private static string ResourceFileName(string resourceName)
        {
            const string prefix = "Firemka.Ksef.Schemas.";
            return resourceName.StartsWith(prefix, StringComparison.Ordinal)
                ? resourceName[prefix.Length..]
                : throw new InvalidOperationException($"Nieznany zasób schematu {resourceName}.");
        }
    }
}
