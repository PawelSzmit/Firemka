using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Firemka.Application.Backups;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Files;

namespace Firemka.Infrastructure.Tests;

public sealed class BackupPayloadWriterTests
{
    [Fact]
    public async Task Payload_contains_dump_files_keys_and_a_verifiable_manifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-payload-{Guid.NewGuid():N}");
        var files = Path.Combine(root, "files");
        var keys = Path.Combine(root, "keys");
        Directory.CreateDirectory(Path.Combine(files, "nested"));
        Directory.CreateDirectory(keys);
        await File.WriteAllTextAsync(Path.Combine(files, "nested", "invoice.xml"), "<Invoice />");
        await File.WriteAllTextAsync(Path.Combine(keys, "key.xml"), "<key />");
        try
        {
            var writer = new BackupPayloadWriter(
                new FakeDatabaseSnapshotSource("synthetic-dump"u8.ToArray()),
                new PrivateFileStoreOptions { RootPath = files },
                new BackupPayloadOptions { DataProtectionKeyRingPath = keys });
            await using var output = new MemoryStream();
            var result = await writer.WriteAsync(
                "owner-1", output, DateTimeOffset.Parse("2026-09-14T12:00:00Z"));

            output.Position = 0;
            using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
            var manifestEntry = Assert.Single(archive.Entries, item => item.FullName == "manifest.json");
            byte[] manifestBytes;
            await using (var manifestStream = manifestEntry.Open())
            await using (var copy = new MemoryStream())
            {
                await manifestStream.CopyToAsync(copy);
                manifestBytes = copy.ToArray();
            }
            Assert.Equal(result.ManifestSha256, Convert.ToHexString(SHA256.HashData(manifestBytes)));
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestBytes,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(manifest);
            Assert.Equal(3, manifest.Entries.Count);
            Assert.Equal(7, manifest.TableRecordCounts["Companies"]);

            foreach (var expected in manifest.Entries)
            {
                var entry = Assert.Single(archive.Entries, item => item.FullName == expected.Path);
                await using var stream = entry.Open();
                await using var copy = new MemoryStream();
                await stream.CopyToAsync(copy);
                Assert.Equal(expected.Length, copy.Length);
                Assert.Equal(expected.Sha256, Convert.ToHexString(SHA256.HashData(copy.ToArray())));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Payload_can_finish_on_async_only_non_seekable_http_stream()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-payload-http-{Guid.NewGuid():N}");
        var files = Path.Combine(root, "files");
        var keys = Path.Combine(root, "keys");
        Directory.CreateDirectory(files);
        Directory.CreateDirectory(keys);
        await File.WriteAllTextAsync(Path.Combine(keys, "key.xml"), "<key />");
        try
        {
            var writer = new BackupPayloadWriter(
                new FakeDatabaseSnapshotSource("synthetic-dump"u8.ToArray()),
                new PrivateFileStoreOptions { RootPath = files },
                new BackupPayloadOptions { DataProtectionKeyRingPath = keys });
            await using var output = new AsyncOnlyWriteStream();

            await writer.WriteAsync("owner-1", output, DateTimeOffset.Parse("2026-09-14T12:00:00Z"));

            using var archive = new ZipArchive(
                new MemoryStream(output.ToArray()), ZipArchiveMode.Read, leaveOpen: false);
            Assert.Contains(archive.Entries, item => item.FullName == "manifest.json");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeDatabaseSnapshotSource(byte[] content) : IBackupDatabaseSnapshotSource
    {
        public async Task<IReadOnlyDictionary<string, long>> WriteConsistentDumpAsync(
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            await destination.WriteAsync(content, cancellationToken);
            return new Dictionary<string, long> { ["Companies"] = 7 };
        }
    }

    private sealed class AsyncOnlyWriteStream : Stream
    {
        private readonly MemoryStream _content = new();

        public byte[] ToArray() => _content.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new InvalidOperationException("Synchronous operations are disallowed.");
        public override Task FlushAsync(CancellationToken cancellationToken)
            => _content.FlushAsync(cancellationToken);
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
            => _content.WriteAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing) _content.Dispose();
            base.Dispose(disposing);
        }
    }
}
