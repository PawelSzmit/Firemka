using System.Net;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Firemka.Infrastructure.Filings;

internal static class FilingSchemaValidator
{
    public static void Validate(string xml, string schemaFileName, string documentLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        var assembly = typeof(FilingSchemaValidator).Assembly;
        var resolver = new ResourceResolver(assembly);
        var schemas = new XmlSchemaSet { XmlResolver = resolver };
        if (schemaFileName.StartsWith("kedu-", StringComparison.OrdinalIgnoreCase))
        {
            using var signatureSchema = resolver.OpenByFileName("xmldsig-core-schema.xsd");
            using var signatureReader = XmlReader.Create(
                signatureSchema,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null },
                "embedded:///xmldsig-core-schema.xsd");
            schemas.Add("http://www.w3.org/2000/09/xmldsig#", signatureReader);
        }

        using var schema = resolver.OpenByFileName(schemaFileName);
        using var schemaReader = XmlReader.Create(
            schema,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = resolver },
            $"embedded:///{schemaFileName}");
        schemas.Add(null, schemaReader);
        schemas.Compile();

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Schemas = schemas,
            ValidationType = ValidationType.Schema,
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        while (reader.Read())
        {
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Dokument {documentLabel} nie jest zgodny z XSD: {string.Join("; ", errors)}");
        }
    }

    private sealed class ResourceResolver : XmlResolver
    {
        private readonly Assembly _assembly;
        private readonly IReadOnlyDictionary<string, string> _resources;

        public ResourceResolver(Assembly assembly)
        {
            _assembly = assembly;
            _resources = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    name => ResourceFileName(name),
                    name => name,
                    StringComparer.OrdinalIgnoreCase);
        }

        public override ICredentials? Credentials { set { } }

        public Stream OpenByFileName(string fileName)
            => _resources.TryGetValue(fileName, out var resource)
                ? _assembly.GetManifestResourceStream(resource)
                    ?? throw new FileNotFoundException($"Nie można otworzyć schematu {fileName}.")
                : throw new FileNotFoundException($"Brakuje osadzonego schematu {fileName}.");

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
            => OpenByFileName(Path.GetFileName(absoluteUri.LocalPath));

        private static string ResourceFileName(string resourceName)
        {
            var marker = resourceName.LastIndexOf("Schemas.", StringComparison.Ordinal);
            return marker >= 0 ? resourceName[(marker + "Schemas.".Length)..] : resourceName;
        }
    }
}
