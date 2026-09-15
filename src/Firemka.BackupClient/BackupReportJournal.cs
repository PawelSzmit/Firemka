using System.Text.Json;
using Firemka.Application.Backups;

namespace Firemka.BackupClient;

internal sealed record PendingBackupReport(string BackupRelativePath, BackupReportCommand Report);

internal static class BackupReportJournal
{
    private const long MaximumJournalLength = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<string> SaveAsync(
        string rootFolder,
        SavedBackup backup,
        BackupReportCommand report,
        CancellationToken cancellationToken)
    {
        var relative = SafeRelativePath(rootFolder, backup.FullPath);
        var finalPath = backup.FullPath + ".pending-report.json";
        var temporaryPath = backup.FullPath + $".pending-report.{Guid.NewGuid():N}.partial";
        try
        {
            await using (var output = new FileStream(
                             temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             16 * 1024, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    output, new PendingBackupReport(relative, report), JsonOptions, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporaryPath, finalPath, overwrite: false);
            return finalPath;
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    public static IEnumerable<string> Enumerate(string rootFolder)
    {
        if (!Directory.Exists(rootFolder)) yield break;
        foreach (var path in Directory.EnumerateFiles(
                     rootFolder, "*.pending-report.json", SearchOption.TopDirectoryOnly))
            yield return path;
        var archives = Path.Combine(rootFolder, "Archives");
        if (!Directory.Exists(archives)) yield break;
        foreach (var path in Directory.EnumerateFiles(
                     archives, "*.pending-report.json", SearchOption.TopDirectoryOnly))
            yield return path;
    }

    public static async Task<PendingBackupReport> ReadAndVerifyAsync(
        string rootFolder,
        string journalPath,
        string recoveryPassword,
        CancellationToken cancellationToken)
    {
        var journalInfo = new FileInfo(journalPath);
        if (!journalInfo.Exists || journalInfo.Length is <= 0 or > MaximumJournalLength
            || (journalInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Lokalny zapis wyniku kopii jest nieprawidłowy.");

        PendingBackupReport pending;
        try
        {
            await using var input = new FileStream(
                journalPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            pending = await JsonSerializer.DeserializeAsync<PendingBackupReport>(
                input, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Lokalny zapis wyniku kopii jest pusty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Lokalny zapis wyniku kopii jest uszkodzony.", exception);
        }

        var backupPath = SafeFullPath(rootFolder, pending.BackupRelativePath);
        if (!File.Exists(backupPath)
            || !string.Equals(Path.GetFileName(backupPath), pending.Report.FileName, StringComparison.Ordinal))
            throw new InvalidDataException("Lokalny zapis wyniku nie odpowiada plikowi kopii.");

        var verificationPath = Path.Combine(
            Path.GetDirectoryName(backupPath)!, $".{Path.GetFileName(backupPath)}.{Guid.NewGuid():N}.report-verify");
        try
        {
            await using var encrypted = new FileStream(
                backupPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var clear = new FileStream(
                verificationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
            await FmbakEncryption.DecryptAsync(encrypted, clear, recoveryPassword, cancellationToken);
            clear.Position = 0;
            var verified = await BackupArchiveVerifier.VerifyAsync(clear, cancellationToken);
            if (!pending.Report.Success
                || pending.Report.FormatVersion != verified.Manifest.FormatVersion
                || !string.Equals(
                    pending.Report.ManifestSha256, verified.ManifestSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Lokalny zapis wyniku nie odpowiada zawartości kopii.");
        }
        finally
        {
            TryDelete(verificationPath);
        }
        return pending;
    }

    public static void Delete(string journalPath) => File.Delete(journalPath);

    private static string SafeRelativePath(string rootFolder, string fullPath)
    {
        var root = Path.GetFullPath(rootFolder);
        var full = Path.GetFullPath(fullPath);
        _ = EnsureInsideRoot(root, full);
        var relative = Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/');
        if (relative.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Ścieżka lokalnej kopii jest niebezpieczna.");
        return relative;
    }

    private static string SafeFullPath(string rootFolder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains('\\')
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Ścieżka lokalnej kopii jest niebezpieczna.");
        return EnsureInsideRoot(Path.GetFullPath(rootFolder), Path.GetFullPath(Path.Combine(rootFolder, relativePath)));
    }

    private static string EnsureInsideRoot(string root, string fullPath)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Ścieżka lokalnej kopii wychodzi poza skonfigurowany folder.");
        return fullPath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}
