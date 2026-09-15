using Firemka.BackupClient;

return await BackupClientProgram.RunAsync(args);

internal static class BackupClientProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                ShowHelp();
                return 0;
            }

            var secrets = new KeychainSecretReader();
            switch (args[0])
            {
                case "run":
                    {
                        var configPath = GetOption(args, "--config") ?? DefaultConfigurationPath();
                        var configuration = await BackupClientConfiguration.LoadAsync(configPath);
                        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                        var results = await new BackupDownloadRunner(http, secrets).RunAsync(
                            configuration, DateTimeOffset.UtcNow);
                        Console.WriteLine(results.Count == 0
                            ? "Kopia jest aktualna."
                            : $"Utworzono i sprawdzono kopie: {results.Count}.");
                        return 0;
                    }
                case "verify":
                    {
                        var input = RequiredOption(args, "--input");
                        var password = await secrets.ReadRecoveryPasswordAsync();
                        var temporary = Path.Combine(Path.GetTempPath(), $"firemka-verify-{Guid.NewGuid():N}.zip");
                        try
                        {
                            await using var encrypted = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
                            await using var clear = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite,
                                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                            await FmbakEncryption.DecryptAsync(encrypted, clear, password);
                            clear.Position = 0;
                            var verified = await BackupArchiveVerifier.VerifyAsync(clear);
                            Console.WriteLine($"Kopia jest poprawna. Wersja: {verified.Manifest.FormatVersion}; wpisy: {verified.Manifest.Entries.Count}.");
                        }
                        finally
                        {
                            if (File.Exists(temporary)) File.Delete(temporary);
                        }
                        return 0;
                    }
                case "restore-extract":
                    {
                        var input = RequiredOption(args, "--input");
                        var output = RequiredOption(args, "--output");
                        var password = await secrets.ReadRecoveryPasswordAsync();
                        var verified = await BackupRestoreExtractor.ExtractVerifiedAsync(input, output, password);
                        Console.WriteLine($"Kopia została sprawdzona i przygotowana do odtworzenia. Wpisy: {verified.Manifest.Entries.Count}.");
                        return 0;
                    }
                default:
                    throw new ArgumentException("Nieznane polecenie klienta kopii.");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Kopia nie powiodła się: {exception.Message}");
            return 1;
        }
    }

    private static string DefaultConfigurationPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "Firemka", "backup-client.json");

    private static string RequiredOption(string[] args, string name)
        => GetOption(args, name) ?? throw new ArgumentException($"Brakuje opcji {name}.");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Firemka Backup Client");
        Console.WriteLine("  run [--config /pełna/ścieżka/backup-client.json]");
        Console.WriteLine("  verify --input /pełna/ścieżka/kopia.fmbak");
        Console.WriteLine("  restore-extract --input /pełna/ścieżka/kopia.fmbak --output /pusty/folder");
    }
}
