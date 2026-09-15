using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Firemka.Application.Backups;

namespace Firemka.BackupClient;

public sealed record VerifiedBackup(BackupManifest Manifest, string ManifestSha256);

public static class BackupArchiveVerifier
{
    public static async Task<VerifiedBackup> VerifyAsync(
        Stream archiveStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        if (!archiveStream.CanRead || !archiveStream.CanSeek)
            throw new ArgumentException("Weryfikacja wymaga czytelnego pliku tymczasowego.", nameof(archiveStream));
        archiveStream.Position = 0;
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > 1_000_000) throw new InvalidDataException("Kopia ma zbyt wiele wpisów.");
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Kopia nie zawiera manifestu.");
        if (archive.Entries.Count(item => item.FullName == "manifest.json") != 1)
            throw new InvalidDataException("Kopia zawiera więcej niż jeden manifest.");
        if (manifestEntry.Length > 50 * 1024 * 1024)
            throw new InvalidDataException("Manifest kopii jest zbyt duży.");
        byte[] manifestBytes;
        await using (var input = manifestEntry.Open())
        await using (var output = new MemoryStream())
        {
            await input.CopyToAsync(output, cancellationToken);
            manifestBytes = output.ToArray();
        }
        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                manifestBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Manifest kopii jest nieprawidłowy.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Manifest kopii jest nieprawidłowy.", exception);
        }
        if (manifest.FormatVersion != 1)
            throw new InvalidDataException($"Nieobsługiwana wersja manifestu: {manifest.FormatVersion}.");
        if (manifest.Entries is null || manifest.TableRecordCounts is null)
            throw new InvalidDataException("Manifest kopii jest niekompletny.");
        if (!manifest.Entries.Any(item => item.Path == "database/firemka.dump"))
            throw new InvalidDataException("Kopia nie zawiera zrzutu bazy.");
        if (!manifest.Entries.Any(item => item.Path.StartsWith("data-protection-keys/", StringComparison.Ordinal)))
            throw new InvalidDataException("Kopia nie zawiera kluczy ochrony danych.");
        foreach (var count in manifest.TableRecordCounts)
        {
            if (string.IsNullOrWhiteSpace(count.Key)
                || !count.Key.All(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                || count.Value < 0)
                throw new InvalidDataException("Manifest zawiera nieprawidłową liczbę rekordów tabeli.");
        }

        var expected = new Dictionary<string, BackupManifestEntry>(StringComparer.Ordinal);
        foreach (var declared in manifest.Entries)
        {
            if (declared is null) throw new InvalidDataException("Manifest zawiera pusty wpis.");
            ValidatePath(declared.Path);
            if (declared.Path == "manifest.json" || declared.Length < 0
                || declared.Sha256 is null || declared.Sha256.Length != 64
                || !declared.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Manifest zawiera nieprawidłową deklarację pliku.");
            if (!expected.TryAdd(declared.Path, declared))
                throw new InvalidDataException("Manifest zawiera powtórzoną ścieżkę.");
        }
        var actual = archive.Entries.Where(item => item.FullName != "manifest.json").ToArray();
        if (actual.Length != expected.Count)
            throw new InvalidDataException("Liczba plików nie odpowiada manifestowi.");
        long totalLength = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in actual)
        {
            ValidatePath(entry.FullName);
            if (!seen.Add(entry.FullName))
                throw new InvalidDataException("Kopia zawiera powtórzoną ścieżkę.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Kopia nie może zawierać dowiązań symbolicznych.");
            if (!expected.TryGetValue(entry.FullName, out var declared))
                throw new InvalidDataException("Kopia zawiera plik spoza manifestu.");
            if (entry.Length != declared.Length)
                throw new InvalidDataException($"Rozmiar wpisu {entry.FullName} nie odpowiada manifestowi.");
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > 1024L * 1024 * 1024 * 1024)
                throw new InvalidDataException("Rozpakowany rozmiar kopii przekracza bezpieczny limit.");
            await using var content = entry.Open();
            var hash = await SHA256.HashDataAsync(content, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(declared.Sha256)))
                throw new InvalidDataException($"Suma pliku {entry.FullName} jest nieprawidłowa.");
        }
        return new VerifiedBackup(manifest, Convert.ToHexString(SHA256.HashData(manifestBytes)));
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains('\\')
            || path.Any(char.IsControl)
            || path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Kopia zawiera niebezpieczną ścieżkę.");
    }
}
