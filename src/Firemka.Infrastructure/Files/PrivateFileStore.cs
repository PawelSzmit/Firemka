using System.Security.Cryptography;
using System.Xml;

namespace Firemka.Infrastructure.Files;

public sealed class PrivateFileStore
{
    private static readonly IReadOnlyDictionary<string, FileTypeDefinition> AllowedFileTypes =
        new Dictionary<string, FileTypeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new(".pdf", "%PDF-"u8.ToArray(), false),
            ["image/jpeg"] = new(".jpg", [0xff, 0xd8, 0xff], false),
            ["image/png"] = new(".png", [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], false),
            ["application/xml"] = new(".xml", [], true),
        };

    private readonly long _maximumFileSizeBytes;
    private readonly string _rootPath;
    private readonly string _rootPathWithSeparator;

    public PrivateFileStore(PrivateFileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            throw new ArgumentException("Ścieżka prywatnego magazynu plików jest wymagana.", nameof(options));
        }

        if (options.MaximumFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Limit rozmiaru pliku musi być dodatni.");
        }

        _rootPath = Path.GetFullPath(options.RootPath);
        _rootPathWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        _maximumFileSizeBytes = options.MaximumFileSizeBytes;
    }

    public async Task<PrivateFileDescriptor> SaveAsync(
        PrivateFileUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(upload.Content);

        if (!AllowedFileTypes.TryGetValue(upload.MediaType, out var fileType))
        {
            throw new InvalidDataException("Ten typ pliku nie jest obsługiwany.");
        }

        Directory.CreateDirectory(_rootPath);
        var id = Guid.NewGuid();
        var fileName = $"{id:N}{fileType.Extension}";
        var storageKey = Path.Combine(fileName[..2], fileName);
        var finalPath = ResolveStoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var temporaryPath = finalPath + ".uploading";

        try
        {
            var header = new byte[fileType.Signature.Length];
            var headerBytesRead = 0;
            long sizeBytes = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81_920];
                while (true)
                {
                    var bytesRead = await upload.Content.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    sizeBytes += bytesRead;
                    if (sizeBytes > _maximumFileSizeBytes)
                    {
                        throw new InvalidDataException($"Plik przekracza limit {_maximumFileSizeBytes} bajtów.");
                    }

                    if (headerBytesRead < header.Length)
                    {
                        var bytesToCopy = Math.Min(bytesRead, header.Length - headerBytesRead);
                        buffer.AsSpan(0, bytesToCopy).CopyTo(header.AsSpan(headerBytesRead));
                        headerBytesRead += bytesToCopy;
                    }

                    hash.AppendData(buffer, 0, bytesRead);
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
            }

            if (sizeBytes == 0)
            {
                throw new InvalidDataException("Plik jest pusty.");
            }

            if (headerBytesRead < fileType.Signature.Length
                || !header.AsSpan().SequenceEqual(fileType.Signature))
            {
                throw new InvalidDataException("Zawartość pliku nie pasuje do zadeklarowanego typu.");
            }

            if (fileType.IsXml)
            {
                await ValidateXmlAsync(temporaryPath, cancellationToken);
            }

            File.Move(temporaryPath, finalPath);
            return new PrivateFileDescriptor(
                storageKey.Replace(Path.DirectorySeparatorChar, '/'),
                Path.GetFileName(upload.OriginalFileName),
                upload.MediaType.ToLowerInvariant(),
                sizeBytes,
                Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public ValueTask<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveStoragePath(storageKey);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    }

    public ValueTask DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveStoragePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Klucz pliku jest wymagany.", nameof(storageKey));
        }

        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        if (!fullPath.StartsWith(_rootPathWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Klucz pliku wskazuje poza prywatny magazyn.");
        }

        return fullPath;
    }

    private static async Task ValidateXmlAsync(string path, CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 20_000_000,
        };

        try
        {
            using var reader = XmlReader.Create(path, settings);
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Plik XML jest niepoprawny lub zawiera niedozwoloną deklarację.", exception);
        }
    }

    private sealed record FileTypeDefinition(string Extension, byte[] Signature, bool IsXml);
}

public sealed record PrivateFileUpload(
    string OriginalFileName,
    string MediaType,
    Stream Content);

public sealed record PrivateFileDescriptor(
    string StorageKey,
    string OriginalFileName,
    string MediaType,
    long SizeBytes,
    string Sha256);
