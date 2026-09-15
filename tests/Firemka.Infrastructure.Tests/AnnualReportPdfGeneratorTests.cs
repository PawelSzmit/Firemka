using Firemka.Application.AnnualClosing;
using Firemka.Domain.AnnualClosing;
using Firemka.Infrastructure.Pdf;

namespace Firemka.Infrastructure.Tests;

public sealed class AnnualReportPdfGeneratorTests
{
    [Fact]
    public void Pdf_contains_all_confirmed_fields_and_polish_characters()
    {
        var values = new AnnualClosingValues(
            120000m, 20000m, 1000m, 500m, 20500m, 0m, 0m, 99500m,
            9000m, 8500m, 100000m, 57672m, 100000m, 9000m, 8700m, 8500m, 300m, 500m);
        var months = Enumerable.Range(1, 12).Select(month => new AnnualMonthSnapshot(
            new DateOnly(2026, month, 1), Guid.NewGuid(), Guid.NewGuid(),
            10_000m, 1_666.67m, 750m, 8_333.33m, 725m)).ToArray();
        var input = new AnnualReportPdfInput(
            "Żółta Łąka", "1234567890", "ul. Źródlana 1, Łódź", "Paweł Żmuda",
            2026, 1, values, months, "Sprawdzenie księgowej 2027-01-10",
            new DateOnly(2027, 1, 10), DateTimeOffset.Parse("2027-01-10T10:00:00Z"),
            "MF PIT-36; MF JPK_PKPIR; ZUS zdrowotna");

        var pdf = new AnnualReportPdfGenerator().Generate(input);

        var outputPath = Environment.GetEnvironmentVariable("FIREMKA_PDF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, pdf);
        }

        Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8));
        Assert.True(pdf.Length > 2_000);
        var text = PdfTestTextExtractor.Extract(pdf);
        Assert.Contains("Żółta Łąka", text);
        Assert.Contains("Paweł Żmuda", text);
        Assert.Contains("Przychody", text);
        Assert.Contains("120 000,00 zł", text);
        Assert.Contains("To nie jest zeznanie PIT", text);
        Assert.Contains("2027-01-10 11:00 czasu polskiego", text);
        Assert.Contains("Strona 1", text);
        Assert.Contains("grudzień 2026", text);
    }
}
