namespace Firemka.BackupClient;

public sealed record SavedBackup(
    string FileName,
    string FullPath,
    string ManifestSha256,
    int FormatVersion);

public static class BackupFileManager
{
    public static async Task<SavedBackup> SaveVerifiedAsync(
        Stream payload,
        string rootFolder,
        string recoveryPassword,
        DateTimeOffset nowUtc,
        int? annualYear = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (string.IsNullOrWhiteSpace(rootFolder) || !Path.IsPathFullyQualified(rootFolder))
            throw new ArgumentException("Folder kopii musi być pełną ścieżką.", nameof(rootFolder));
        var destinationFolder = annualYear is null ? rootFolder : Path.Combine(rootFolder, "Archives");
        Directory.CreateDirectory(destinationFolder);
        var stamp = nowUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = annualYear is null
            ? $"firemka-backup-{stamp}-v1.fmbak"
            : $"firemka-archive-{annualYear}-{stamp}-v1.fmbak";
        var finalPath = Path.Combine(destinationFolder, fileName);
        var partialPath = Path.Combine(destinationFolder, $".{fileName}.{Guid.NewGuid():N}.partial");
        var verificationPath = Path.Combine(destinationFolder, $".{fileName}.{Guid.NewGuid():N}.verify");
        var movedToFinal = false;
        try
        {
            await using (var encrypted = new FileStream(
                             partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await FmbakEncryption.EncryptAsync(payload, encrypted, recoveryPassword, cancellationToken);
                await encrypted.FlushAsync(cancellationToken);
                encrypted.Flush(flushToDisk: true);
            }

            await using (var encrypted = new FileStream(
                             partialPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var clear = new FileStream(
                             verificationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose))
            {
                await FmbakEncryption.DecryptAsync(encrypted, clear, recoveryPassword, cancellationToken);
                clear.Position = 0;
                var verified = await BackupArchiveVerifier.VerifyAsync(clear, cancellationToken);
                File.Move(partialPath, finalPath, overwrite: false);
                movedToFinal = true;
                if (annualYear is null) RotateDaily(rootFolder, keep: 5);
                return new SavedBackup(fileName, finalPath, verified.ManifestSha256, verified.Manifest.FormatVersion);
            }
        }
        catch
        {
            TryDelete(partialPath);
            TryDelete(verificationPath);
            if (movedToFinal) TryDelete(finalPath);
            throw;
        }
    }

    public static void RotateDaily(string rootFolder, int keep)
    {
        if (keep <= 0) throw new ArgumentOutOfRangeException(nameof(keep));
        var candidates = Directory.EnumerateFiles(rootFolder, "firemka-backup-*-v*.fmbak", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        foreach (var old in candidates.Skip(keep)) old.Delete();
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
