using System.IO.Compression;

namespace Firemka.BackupClient;

public static class BackupRestoreExtractor
{
    public static async Task<VerifiedBackup> ExtractVerifiedAsync(
        string encryptedPath,
        string emptyDestination,
        string recoveryPassword,
        CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(encryptedPath) || !File.Exists(encryptedPath))
            throw new FileNotFoundException("Nie znaleziono pliku .fmbak.", encryptedPath);
        if (!Path.IsPathFullyQualified(emptyDestination))
            throw new ArgumentException("Folder odtworzenia musi być pełną ścieżką.", nameof(emptyDestination));
        if (Directory.Exists(emptyDestination) && Directory.EnumerateFileSystemEntries(emptyDestination).Any())
            throw new InvalidOperationException("Folder odtworzenia nie jest pusty.");
        var createdFiles = new List<string>();
        var createdDirectories = new List<string>();
        if (!Directory.Exists(emptyDestination))
        {
            Directory.CreateDirectory(emptyDestination);
            createdDirectories.Add(Path.GetFullPath(emptyDestination));
        }
        var temporary = Path.Combine(Path.GetTempPath(), $"firemka-restore-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var encrypted = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var clear = new FileStream(
                             temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                             128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await FmbakEncryption.DecryptAsync(encrypted, clear, recoveryPassword, cancellationToken);
                clear.Position = 0;
                var verified = await BackupArchiveVerifier.VerifyAsync(clear, cancellationToken);
                clear.Position = 0;
                using var archive = new ZipArchive(clear, ZipArchiveMode.Read, leaveOpen: true);
                var destinationRoot = Path.GetFullPath(emptyDestination) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    var destination = Path.GetFullPath(Path.Combine(
                        emptyDestination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(destinationRoot, StringComparison.Ordinal))
                        throw new InvalidDataException("Kopia zawiera ścieżkę poza folderem odtworzenia.");
                    CreateOwnedDirectories(
                        Path.GetDirectoryName(destination)!,
                        Path.GetFullPath(emptyDestination),
                        createdDirectories);
                    await using var source = entry.Open();
                    await using var target = new FileStream(
                        destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    createdFiles.Add(destination);
                    await source.CopyToAsync(target, cancellationToken);
                }
                return verified;
            }
        }
        catch
        {
            foreach (var path in createdFiles.AsEnumerable().Reverse()) TryDeleteFile(path);
            foreach (var path in createdDirectories.OrderByDescending(path => path.Length))
                TryDeleteEmptyDirectory(path);
            throw;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void CreateOwnedDirectories(
        string directory,
        string destinationRoot,
        ICollection<string> createdDirectories)
    {
        var missing = new Stack<string>();
        for (var current = directory;
             !Directory.Exists(current) && current.StartsWith(destinationRoot, StringComparison.Ordinal);
             current = Path.GetDirectoryName(current)!)
        {
            missing.Push(current);
        }

        while (missing.TryPop(out var path))
        {
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: false);
        }
        catch
        {
        }
    }
}
