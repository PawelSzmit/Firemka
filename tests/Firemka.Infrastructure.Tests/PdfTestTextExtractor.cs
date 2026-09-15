using System.Diagnostics;

namespace Firemka.Infrastructure.Tests;

internal static class PdfTestTextExtractor
{
    public static string Extract(byte[] pdf)
    {
        var path = Path.Combine(Path.GetTempPath(), $"firemka-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, pdf);
            var startInfo = new ProcessStartInfo("pdftotext", ["-layout", path, "-"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Nie uruchomiono pdftotext.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            return output;
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
