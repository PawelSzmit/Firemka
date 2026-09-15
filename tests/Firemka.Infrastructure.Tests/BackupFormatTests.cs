using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Firemka.Application.Backups;
using Firemka.BackupClient;

namespace Firemka.Infrastructure.Tests;

public sealed class BackupFormatTests
{
    private const string Password = "synthetic-recovery-password-123!";

    [Fact]
    public async Task Chunked_authenticated_format_round_trips_and_rejects_wrong_password_corruption_and_truncation()
    {
        var clear = RandomNumberGenerator.GetBytes(2_300_000);
        await using var encrypted = new MemoryStream();
        await FmbakEncryption.EncryptAsync(new MemoryStream(clear), encrypted, Password);
        var bytes = encrypted.ToArray();

        await using var restored = new MemoryStream();
        await FmbakEncryption.DecryptAsync(new MemoryStream(bytes), restored, Password);
        Assert.Equal(clear, restored.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() => FmbakEncryption.DecryptAsync(
            new MemoryStream(bytes), new MemoryStream(), "different-recovery-password!"));
        var corrupted = bytes.ToArray();
        corrupted[100_000] ^= 0x5A;
        await Assert.ThrowsAsync<InvalidDataException>(() => FmbakEncryption.DecryptAsync(
            new MemoryStream(corrupted), new MemoryStream(), Password));
        await Assert.ThrowsAsync<InvalidDataException>(() => FmbakEncryption.DecryptAsync(
            new MemoryStream(bytes[..^10]), new MemoryStream(), Password));
    }

    [Fact]
    public async Task Verified_save_is_atomic_rotates_daily_five_to_five_and_never_rotates_annual_archive()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-format-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var day in Enumerable.Range(1, 5))
                await File.WriteAllTextAsync(
                    Path.Combine(root, $"firemka-backup-2026090{day}T000000000Z-v1.fmbak"), "old");
            var archives = Path.Combine(root, "Archives");
            Directory.CreateDirectory(archives);
            var annual = Path.Combine(archives, "firemka-archive-2025-old-v1.fmbak");
            await File.WriteAllTextAsync(annual, "annual");

            var payload = await CreatePayloadAsync();
            var saved = await BackupFileManager.SaveVerifiedAsync(
                new MemoryStream(payload), root, Password,
                DateTimeOffset.Parse("2026-09-14T12:00:00Z"));

            Assert.True(File.Exists(saved.FullPath));
            Assert.Equal(5, Directory.GetFiles(root, "firemka-backup-*.fmbak").Length);
            Assert.True(File.Exists(annual));
            Assert.Empty(Directory.GetFiles(root, "*.partial"));
            Assert.Empty(Directory.GetFiles(root, "*.verify"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Interrupted_download_leaves_no_final_or_partial_file_and_restore_refuses_nonempty_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-interrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var payload = await CreatePayloadAsync();
            await Assert.ThrowsAsync<IOException>(() => BackupFileManager.SaveVerifiedAsync(
                new InterruptingReadStream(payload, payload.Length / 2), root, Password,
                DateTimeOffset.Parse("2026-09-14T12:00:00Z")));
            Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));

            await File.WriteAllTextAsync(Path.Combine(root, "existing.txt"), "do not overwrite");
            var existingBackup = Path.Combine(root, "existing.fmbak");
            await File.WriteAllBytesAsync(existingBackup, [1, 2, 3]);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BackupRestoreExtractor.ExtractVerifiedAsync(
                    existingBackup, root, Password));
            Assert.Contains("pusty", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("do not overwrite", await File.ReadAllTextAsync(Path.Combine(root, "existing.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Cancelled_restore_preserves_foreign_file_added_after_extraction_started()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-cancelled-restore-{Guid.NewGuid():N}");
        var encryptedPath = Path.Combine(Path.GetTempPath(), $"firemka-cancelled-restore-{Guid.NewGuid():N}.fmbak");
        try
        {
            var payload = await CreatePayloadAsync();
            await using (var encrypted = new FileStream(encryptedPath, FileMode.CreateNew, FileAccess.Write))
                await FmbakEncryption.EncryptAsync(new MemoryStream(payload), encrypted, Password);

            using var cancellation = new CancellationTokenSource();
            var extraction = BackupRestoreExtractor.ExtractVerifiedAsync(
                encryptedPath, root, Password, cancellation.Token);
            Assert.True(SpinWait.SpinUntil(() => Directory.Exists(root), TimeSpan.FromSeconds(5)));
            var foreignPath = Path.Combine(root, "foreign.txt");
            await File.WriteAllTextAsync(foreignPath, "nie należy do procesu odtworzenia");
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extraction);
            Assert.Equal("nie należy do procesu odtworzenia", await File.ReadAllTextAsync(foreignPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            if (File.Exists(encryptedPath)) File.Delete(encryptedPath);
        }
    }

    [Fact]
    public async Task Full_disk_write_failure_is_not_reported_as_a_valid_encrypted_backup()
    {
        var error = await Assert.ThrowsAsync<IOException>(() => FmbakEncryption.EncryptAsync(
            new MemoryStream(new byte[4096]),
            new QuotaWriteStream(100),
            Password));
        Assert.Contains("full", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Annual_save_uses_archives_folder_and_is_not_removed_by_daily_rotation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"firemka-annual-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var payload = await CreatePayloadAsync();
            var annual = await BackupFileManager.SaveVerifiedAsync(
                new MemoryStream(payload), root, Password,
                DateTimeOffset.Parse("2026-09-14T12:00:00Z"), annualYear: 2025);
            foreach (var day in Enumerable.Range(1, 6))
                await File.WriteAllTextAsync(
                    Path.Combine(root, $"firemka-backup-2026090{day}T000000000Z-v1.fmbak"), "old");

            BackupFileManager.RotateDaily(root, keep: 5);

            Assert.True(File.Exists(annual.FullPath));
            Assert.Equal(Path.Combine(root, "Archives"), Path.GetDirectoryName(annual.FullPath));
            Assert.Equal(5, Directory.GetFiles(root, "firemka-backup-*.fmbak").Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Verifier_rejects_duplicate_manifest_paths_and_malformed_hash_as_invalid_data()
    {
        var malformed = await CreatePayloadAsync(
            entries => [entries[0], entries[0], .. entries.Skip(1)]);
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupArchiveVerifier.VerifyAsync(
            new MemoryStream(malformed)));

        var badHash = await CreatePayloadAsync(
            entries => [entries[0] with { Sha256 = "not-a-sha" }, .. entries.Skip(1)]);
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupArchiveVerifier.VerifyAsync(
            new MemoryStream(badHash)));
    }

    private static async Task<byte[]> CreatePayloadAsync(
        Func<IReadOnlyList<BackupManifestEntry>, IReadOnlyList<BackupManifestEntry>>? changeEntries = null)
    {
        var content = new Dictionary<string, byte[]>
        {
            ["database/firemka.dump"] = "synthetic database"u8.ToArray(),
            ["private-files/a.xml"] = "<Invoice />"u8.ToArray(),
            ["data-protection-keys/key.xml"] = "<key />"u8.ToArray(),
        };
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in content)
            {
                var entry = archive.CreateEntry(item.Key);
                await using var stream = entry.Open();
                await stream.WriteAsync(item.Value);
            }
            var entries = content.Select(item => new BackupManifestEntry(
                item.Key, item.Value.Length, Convert.ToHexString(SHA256.HashData(item.Value)))).ToArray();
            var manifest = new BackupManifest(
                1,
                DateTimeOffset.Parse("2026-09-14T12:00:00Z"),
                "test",
                changeEntries?.Invoke(entries) ?? entries,
                new Dictionary<string, long> { ["Companies"] = 1 });
            var manifestEntry = archive.CreateEntry("manifest.json");
            await using var manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(
                manifestStream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        return output.ToArray();
    }

    private sealed class InterruptingReadStream(byte[] content, int failAfter) : MemoryStream(content)
    {
        private int _read;

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_read >= failAfter) throw new IOException("Synthetic interrupted download.");
            var allowed = Math.Min(buffer.Length, failAfter - _read);
            var read = await base.ReadAsync(buffer[..allowed], cancellationToken);
            _read += read;
            return read;
        }
    }

    private sealed class QuotaWriteStream(int remaining) : MemoryStream
    {
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.Length > remaining) throw new IOException("Synthetic full disk.");
            remaining -= buffer.Length;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
