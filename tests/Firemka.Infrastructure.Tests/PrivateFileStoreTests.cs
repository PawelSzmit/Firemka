using System.Security.Cryptography;
using Firemka.Infrastructure.Files;

namespace Firemka.Infrastructure.Tests;

public sealed class PrivateFileStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        $"firemka-private-files-{Guid.NewGuid():N}");

    [Fact]
    public async Task Valid_pdf_is_stored_under_a_random_key_with_a_checksum()
    {
        var bytes = "%PDF-1.7\nsynthetic test document"u8.ToArray();
        var store = CreateStore(maximumBytes: 1_024);

        var descriptor = await store.SaveAsync(
            new PrivateFileUpload(
                "../../sensitive-invoice.pdf",
                "application/pdf",
                new MemoryStream(bytes)));

        Assert.DoesNotContain("sensitive-invoice", descriptor.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(bytes.Length, descriptor.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), descriptor.Sha256);
        Assert.Equal("application/pdf", descriptor.MediaType);

        await using var storedContent = await store.OpenReadAsync(descriptor.StorageKey);
        using var copy = new MemoryStream();
        await storedContent.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task File_with_a_false_media_type_is_rejected_without_leaving_content()
    {
        var store = CreateStore(maximumBytes: 1_024);
        var upload = new PrivateFileUpload(
            "malware.pdf",
            "application/pdf",
            new MemoryStream("MZ executable"u8.ToArray()));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(upload));

        Assert.Empty(GetStoredFiles());
    }

    [Fact]
    public async Task File_over_the_size_limit_is_rejected_without_leaving_content()
    {
        var store = CreateStore(maximumBytes: 8);
        var upload = new PrivateFileUpload(
            "too-large.pdf",
            "application/pdf",
            new MemoryStream("%PDF-1.7-too-large"u8.ToArray()));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(upload));

        Assert.Empty(GetStoredFiles());
    }

    [Fact]
    public async Task Ksef_xml_is_accepted_but_a_doctype_payload_is_rejected()
    {
        var store = CreateStore(maximumBytes: 4_096);
        var valid = await store.SaveAsync(new PrivateFileUpload(
            "invoice.xml",
            "application/xml",
            new MemoryStream("<Invoice><Number>FV/1</Number></Invoice>"u8.ToArray())));

        Assert.Equal("application/xml", valid.MediaType);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new PrivateFileUpload(
            "unsafe.xml",
            "application/xml",
            new MemoryStream("<!DOCTYPE Invoice [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><Invoice>&xxe;</Invoice>"u8.ToArray()))));

        Assert.Single(GetStoredFiles());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private PrivateFileStore CreateStore(long maximumBytes)
    {
        return new PrivateFileStore(new PrivateFileStoreOptions
        {
            RootPath = _rootPath,
            MaximumFileSizeBytes = maximumBytes,
        });
    }

    private string[] GetStoredFiles()
    {
        return Directory.Exists(_rootPath)
            ? Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories)
            : [];
    }
}
