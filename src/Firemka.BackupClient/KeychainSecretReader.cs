using System.Diagnostics;

namespace Firemka.BackupClient;

public interface IBackupSecretReader
{
    Task<string> ReadTokenAsync(CancellationToken cancellationToken = default);
    Task<string> ReadRecoveryPasswordAsync(CancellationToken cancellationToken = default);
}

public sealed class KeychainSecretReader : IBackupSecretReader
{
    public Task<string> ReadTokenAsync(CancellationToken cancellationToken = default)
        => ReadAsync("pl.firemka.backup.token", cancellationToken);

    public Task<string> ReadRecoveryPasswordAsync(CancellationToken cancellationToken = default)
        => ReadAsync("pl.firemka.backup.recovery", cancellationToken);

    private static async Task<string> ReadAsync(string service, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Pęk kluczy klienta kopii jest dostępny wyłącznie w macOS.");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("find-generic-password");
        process.StartInfo.ArgumentList.Add("-w");
        process.StartInfo.ArgumentList.Add("-s");
        process.StartInfo.ArgumentList.Add(service);
        process.StartInfo.ArgumentList.Add("-a");
        process.StartInfo.ArgumentList.Add(Environment.UserName);
        if (!process.Start()) throw new InvalidOperationException("Nie udało się odczytać Pęku kluczy.");
        var valueTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var value = (await valueTask).TrimEnd('\r', '\n');
        var error = await errorTask;
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Brakuje sekretu {service} w Pęku kluczy. {SafeError(error)}".Trim());
        return value;
    }

    private static string SafeError(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 200 ? normalized : normalized[..200];
    }
}
