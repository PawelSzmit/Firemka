using System.Text.Json;

namespace Firemka.BackupClient;

public sealed record BackupClientConfiguration(Uri ServerUrl, string BackupFolder)
{
    public static async Task<BackupClientConfiguration> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Ścieżka konfiguracji musi być pełna.", nameof(path));
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var value = await JsonSerializer.DeserializeAsync<ConfigurationFile>(
            stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken)
            ?? throw new InvalidDataException("Konfiguracja klienta kopii jest pusta.");
        if (!Uri.TryCreate(value.ServerUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Adres Firemki musi używać HTTPS.");
        if (string.IsNullOrWhiteSpace(value.BackupFolder) || !Path.IsPathFullyQualified(value.BackupFolder))
            throw new InvalidDataException("Folder kopii musi być pełną ścieżką.");
        return new BackupClientConfiguration(uri, value.BackupFolder);
    }

    private sealed record ConfigurationFile(string ServerUrl, string BackupFolder);
}
