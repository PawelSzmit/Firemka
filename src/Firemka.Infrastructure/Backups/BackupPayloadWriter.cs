using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Firemka.Application.Backups;
using Firemka.Infrastructure.Files;

namespace Firemka.Infrastructure.Backups;

public sealed class BackupPayloadWriter(
    IBackupDatabaseSnapshotSource database,
    PrivateFileStoreOptions privateFiles,
    BackupPayloadOptions options) : IBackupPayloadWriter
{
    public const int CurrentFormatVersion = 1;

    public async Task<BackupPayloadResult> WriteAsync(
        string ownerUserId,
        Stream destination,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("Strumień kopii nie jest zapisywalny.", nameof(destination));
        ValidateRoot(privateFiles.RootPath, "prywatnych plików");
        ValidateRoot(options.DataProtectionKeyRingPath, "kluczy ochrony danych");

        var entries = new List<BackupManifestEntry>();
        IReadOnlyDictionary<string, long> counts;
        BackupPayloadResult result;
        await using var zipDestination = new AsyncMetadataBufferingStream(destination);
        await using (var archive = await ZipArchive.CreateAsync(
                         zipDestination, ZipArchiveMode.Create, leaveOpen: true,
                         entryNameEncoding: null, cancellationToken))
        {
            var dumpEntry = archive.CreateEntry("database/firemka.dump", CompressionLevel.NoCompression);
            dumpEntry.LastWriteTime = nowUtc;
            await using (var dumpStream = await dumpEntry.OpenAsync(cancellationToken))
            {
                using var hashing = new HashingWriteStream(dumpStream);
                counts = await database.WriteConsistentDumpAsync(hashing, cancellationToken);
                entries.Add(hashing.Complete("database/firemka.dump"));
            }

            await AddTreeAsync(archive, privateFiles.RootPath, "private-files", entries, nowUtc, cancellationToken);
            await AddTreeAsync(archive, options.DataProtectionKeyRingPath, "data-protection-keys", entries, nowUtc, cancellationToken);

            var manifest = new BackupManifest(
                CurrentFormatVersion,
                nowUtc,
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                entries.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray(),
                new SortedDictionary<string, long>(
                    counts.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal));
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            manifestEntry.LastWriteTime = nowUtc;
            await using var manifestStream = await manifestEntry.OpenAsync(cancellationToken);
            await manifestStream.WriteAsync(manifestBytes, cancellationToken);
            var manifestSha = Convert.ToHexString(SHA256.HashData(manifestBytes));
            result = new BackupPayloadResult(CurrentFormatVersion, manifestSha, nowUtc);
        }
        await zipDestination.FlushAsync(cancellationToken);
        return result;
    }

    private static async Task AddTreeAsync(
        ZipArchive archive,
        string root,
        string prefix,
        List<BackupManifestEntry> entries,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var info = new FileInfo(file);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Kopia nie może zawierać dowiązań symbolicznych.");
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) || relative == "..")
                throw new InvalidOperationException("Plik kopii znajduje się poza dozwolonym folderem.");
            var path = $"{prefix}/{relative}";
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            entry.LastWriteTime = nowUtc;
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read,
                128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var output = await entry.OpenAsync(cancellationToken);
            using var hashing = new HashingWriteStream(output);
            await input.CopyToAsync(hashing, cancellationToken);
            entries.Add(hashing.Complete(path));
        }
    }

    private static void ValidateRoot(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
            throw new InvalidOperationException($"Brakuje poprawnego folderu {label} do pełnej kopii.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private sealed class HashingWriteStream(Stream destination) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _length;
        private bool _completed;

        public BackupManifestEntry Complete(string path)
        {
            if (_completed) throw new InvalidOperationException("Skrót wpisu został już zakończony.");
            _completed = true;
            return new BackupManifestEntry(path, _length, Convert.ToHexString(_hash.GetHashAndReset()));
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_completed) throw new InvalidOperationException("Nie można dopisywać po zakończeniu skrótu.");
            _hash.AppendData(buffer.Span);
            _length += buffer.Length;
            await destination.WriteAsync(buffer, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_completed) throw new InvalidOperationException("Nie można dopisywać po zakończeniu skrótu.");
            _hash.AppendData(buffer, offset, count);
            _length += count;
            destination.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hash.Dispose();
            base.Dispose(disposing);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => destination.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => destination.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => destination.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // ZipArchive nadal zapisuje małe nagłówki i deskryptory synchronicznie, nawet
    // przy nowych metodach asynchronicznych. Bufor zachowuje ich kolejność, a do
    // odpowiedzi HTTP przekazuje dane wyłącznie przez WriteAsync/FlushAsync.
    private sealed class AsyncMetadataBufferingStream(Stream destination) : Stream, IAsyncDisposable
    {
        private const int MaximumPendingBytes = 16 * 1024 * 1024;
        private readonly MemoryStream _pending = new();
        private bool _disposed;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed && destination.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
            => Buffer(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer) => Buffer(buffer);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await FlushPendingAsync(cancellationToken);
            await destination.WriteAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() => ThrowIfDisposed();

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            await FlushPendingAsync(cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            await FlushPendingAsync(CancellationToken.None);
            _disposed = true;
            _pending.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
                throw new InvalidOperationException("Strumień pakietu kopii wymaga asynchronicznego zamknięcia.");
            base.Dispose(disposing);
        }

        private void Buffer(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            if (_pending.Length + buffer.Length > MaximumPendingBytes)
                throw new InvalidOperationException("Metadane pakietu ZIP przekroczyły bezpieczny limit.");
            _pending.Write(buffer);
        }

        private async Task FlushPendingAsync(CancellationToken cancellationToken)
        {
            if (_pending.Length == 0) return;
            if (!_pending.TryGetBuffer(out var buffered))
                throw new InvalidOperationException("Nie można odczytać bufora metadanych ZIP.");
            await destination.WriteAsync(
                buffered.AsMemory(0, checked((int)_pending.Length)), cancellationToken);
            _pending.SetLength(0);
            _pending.Position = 0;
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
