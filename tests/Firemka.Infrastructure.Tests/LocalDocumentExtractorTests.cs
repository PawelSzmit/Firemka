using System.Text.Json;
using Firemka.Application.ExternalServices;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Ocr;

namespace Firemka.Infrastructure.Tests;

public sealed class LocalDocumentExtractorTests
{
    [Fact]
    public async Task Parsed_values_are_suggestions_with_a_non_final_confidence()
    {
        var extractor = new LocalDocumentExtractor(new FixedTextEngine(
            """
            Faktura VAT nr: FV/123/2026
            Sprzedawca: Sztuczny Dostawca sp. z o.o.
            NIP: 1234567890
            Data wystawienia: 09.09.2026
            Razem brutto: 1 234,56 PLN
            """));

        var result = await extractor.ExtractAsync(new DocumentExtractionRequest(
            Guid.NewGuid(),
            "invoice.pdf",
            "application/pdf",
            "%PDF-synthetic"u8.ToArray()));
        var data = JsonSerializer.Deserialize<DocumentData>(result.FieldsJson);

        Assert.Equal("Extracted", result.Status);
        Assert.NotNull(data);
        Assert.Equal("FV/123/2026", data.InvoiceNumber);
        Assert.Equal("Sztuczny Dostawca sp. z o.o.", data.SellerName);
        Assert.Equal(1234.56m, data.GrossAmount);
        Assert.InRange(result.Confidence!.Value, 0.5m, 0.99m);
        var confidences = JsonSerializer.Deserialize<Dictionary<string, decimal>>(result.FieldConfidencesJson!);
        Assert.True(confidences![nameof(DocumentData.GrossAmount)] < 1m);
    }

    [Fact]
    public async Task Empty_ocr_result_is_sent_to_manual_review_instead_of_being_invented()
    {
        var extractor = new LocalDocumentExtractor(new FixedTextEngine(string.Empty));

        var result = await extractor.ExtractAsync(new DocumentExtractionRequest(
            Guid.NewGuid(),
            "scan.jpg",
            "image/jpeg",
            new byte[] { 0xff, 0xd8, 0xff }));
        var data = JsonSerializer.Deserialize<DocumentData>(result.FieldsJson);

        Assert.Equal("NeedsManualReview", result.Status);
        Assert.Equal(DocumentData.Empty, data);
        Assert.Null(result.Confidence);
    }

    [Fact]
    public async Task Real_local_tools_read_the_rendered_synthetic_pdf_and_photo_when_available()
    {
        Assert.True(IsCommandAvailable("pdftotext"), "Brakuje pdftotext w PATH; rzeczywisty test PDF nie został wykonany.");
        Assert.True(IsCommandAvailable("tesseract"), "Brakuje Tesseract w PATH; rzeczywisty test obrazu nie został wykonany.");

        var repository = FindRepositoryRoot();
        var extractor = new LocalDocumentExtractor(new ProcessDocumentTextExtractionEngine());
        var pdfPath = Path.Combine(repository, "tests", "Fixtures", "phase4", "phase4-synthetic-invoice.pdf");
        var photoPath = Path.Combine(repository, "tests", "Fixtures", "phase4", "phase4-synthetic-invoice-photo.png");
        Assert.True(File.Exists(pdfPath));
        Assert.True(File.Exists(photoPath));

        var pdfResult = await extractor.ExtractAsync(new DocumentExtractionRequest(
            Guid.NewGuid(),
            Path.GetFileName(pdfPath),
            "application/pdf",
            await File.ReadAllBytesAsync(pdfPath)));
        var photoResult = await extractor.ExtractAsync(new DocumentExtractionRequest(
            Guid.NewGuid(),
            Path.GetFileName(photoPath),
            "image/png",
            await File.ReadAllBytesAsync(photoPath)));
        var pdfData = JsonSerializer.Deserialize<DocumentData>(pdfResult.FieldsJson);
        var photoData = JsonSerializer.Deserialize<DocumentData>(photoResult.FieldsJson);

        Assert.Equal("FV/123/2026", pdfData!.InvoiceNumber);
        Assert.Equal(1234.56m, pdfData.GrossAmount);
        Assert.Equal("FV/123/2026", photoData!.InvoiceNumber);
        Assert.Equal(1234.56m, photoData.GrossAmount);
    }

    [Fact]
    public async Task Cancelling_ocr_kills_the_external_process_tree()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Ten test procesu docelowo działa na macOS i Linuxie.");
        }
        var testDirectory = Path.Combine(Path.GetTempPath(), $"firemka-ocr-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var scriptPath = Path.Combine(testDirectory, "blocking-tesseract.sh");
        var pidPath = Path.Combine(testDirectory, "pid");
        await File.WriteAllTextAsync(
            scriptPath,
            "#!/bin/sh\necho $$ > \"$(dirname \"$0\")/pid\"\nsleep 30\n");
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var engine = new ProcessDocumentTextExtractionEngine(
            "pdftotext",
            "pdftoppm",
            scriptPath);
        using var cancellation = new CancellationTokenSource();

        try
        {
            var extraction = engine.ExtractTextAsync(
                "scan.png",
                "image/png",
                new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a },
                cancellation.Token);
            for (var attempt = 0; attempt < 200 && !File.Exists(pidPath) && !extraction.IsCompleted; attempt++)
            {
                await Task.Delay(25);
            }

            if (!File.Exists(pidPath) && extraction.IsCompleted)
            {
                _ = await extraction;
            }

            Assert.True(File.Exists(pidPath), "Proces testowy OCR nie wystartował.");
            var processId = int.Parse(await File.ReadAllTextAsync(pidPath), System.Globalization.CultureInfo.InvariantCulture);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extraction);
            Assert.False(IsProcessRunning(processId), "Anulowany proces OCR nadal działa.");
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Firemka.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Nie znaleziono katalogu repozytorium.");
    }

    private static bool IsCommandAvailable(string command) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory, command))
        .Any(File.Exists);

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class FixedTextEngine(string text) : IDocumentTextExtractionEngine
    {
        public Task<string> ExtractTextAsync(
            string fileName,
            string mediaType,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(text);
    }
}
