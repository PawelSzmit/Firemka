using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Firemka.Infrastructure.Ocr;

public sealed class ProcessDocumentTextExtractionEngine : IDocumentTextExtractionEngine
{
    private const int MaximumTextLength = 2_000_000;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(45);
    private readonly string _pdfToTextExecutable;
    private readonly string _pdfToPpmExecutable;
    private readonly string _tesseractExecutable;

    public ProcessDocumentTextExtractionEngine()
        : this("pdftotext", "pdftoppm", "tesseract")
    {
    }

    public ProcessDocumentTextExtractionEngine(
        string pdfToTextExecutable,
        string pdfToPpmExecutable,
        string tesseractExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfToTextExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfToPpmExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(tesseractExecutable);
        _pdfToTextExecutable = pdfToTextExecutable;
        _pdfToPpmExecutable = pdfToPpmExecutable;
        _tesseractExecutable = tesseractExecutable;
    }

    public async Task<string> ExtractTextAsync(
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var extension = mediaType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new InvalidDataException("Ten typ dokumentu nie jest obsługiwany przez OCR."),
        };
        if (content.IsEmpty)
        {
            throw new InvalidDataException("Plik dokumentu jest pusty.");
        }

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "firemka-ocr",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var inputPath = Path.Combine(workingDirectory, "source" + extension);

        try
        {
            await File.WriteAllBytesAsync(inputPath, content.ToArray(), cancellationToken);
            if (mediaType != "application/pdf")
            {
                return await RunTesseractAsync(inputPath, cancellationToken);
            }

            var text = await RunCommandAsync(
                _pdfToTextExecutable,
                ["-layout", inputPath, "-"],
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var pagePrefix = Path.Combine(workingDirectory, "page");
            await RunCommandAsync(
                _pdfToPpmExecutable,
                ["-f", "1", "-l", "3", "-r", "200", "-png", inputPath, pagePrefix],
                cancellationToken);
            var pages = Directory.GetFiles(workingDirectory, "page-*.png")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var combined = new StringBuilder();
            foreach (var page in pages)
            {
                combined.AppendLine(await RunTesseractAsync(page, cancellationToken));
                if (combined.Length > MaximumTextLength)
                {
                    throw new InvalidDataException("Wynik OCR jest zbyt duży.");
                }
            }

            return combined.ToString();
        }
        finally
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
    }

    private Task<string> RunTesseractAsync(
        string inputPath,
        CancellationToken cancellationToken) =>
        RunCommandAsync(_tesseractExecutable, [inputPath, "stdout", "-l", "pol+eng"], cancellationToken);

    private static async Task<string> RunCommandAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Nie udało się uruchomić lokalnego narzędzia OCR.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "Brakuje lokalnego narzędzia OCR. Sprawdź instalację pdftotext i Tesseract.",
                exception);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException("Lokalne rozpoznawanie dokumentu przekroczyło limit czasu.");
        }

        var output = await outputTask;
        _ = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"Lokalne narzędzie OCR zakończyło się kodem {process.ExitCode}.");
        }

        if (output.Length > MaximumTextLength)
        {
            throw new InvalidDataException("Wynik OCR jest zbyt duży.");
        }

        return output;
    }
}
