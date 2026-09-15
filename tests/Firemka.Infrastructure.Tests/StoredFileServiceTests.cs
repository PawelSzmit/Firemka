using Firemka.Infrastructure.Files;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class StoredFileServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        $"firemka-stored-files-{Guid.NewGuid():N}");

    [Fact]
    public async Task Stored_file_can_only_be_opened_by_its_owner()
    {
        await using var dbContext = CreateDbContext();
        var privateStore = new PrivateFileStore(new PrivateFileStoreOptions
        {
            RootPath = _rootPath,
            MaximumFileSizeBytes = 1_024,
        });
        var service = new StoredFileService(dbContext, privateStore);
        const string ownerId = "owner-1";

        var stored = await service.StoreAsync(
            ownerId,
            new PrivateFileUpload(
                "invoice.pdf",
                "application/pdf",
                new MemoryStream("%PDF-1.7\nsynthetic"u8.ToArray())),
            new StoredFileContext(
                StoredFileOrigin.ManualUpload,
                StoredFileRecordType.SourceDocument,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                1));

        Assert.Null(await service.OpenAsync(stored.Id, "different-owner"));
        var opened = await service.OpenAsync(stored.Id, ownerId);
        Assert.NotNull(opened);
        await using (opened.Content)
        {
            Assert.Equal("application/pdf", opened.MediaType);
            Assert.Equal("invoice.pdf", opened.OriginalFileName);
            using var copy = new MemoryStream();
            await opened.Content.CopyToAsync(copy);
            Assert.StartsWith("%PDF-", System.Text.Encoding.UTF8.GetString(copy.ToArray()), StringComparison.Ordinal);
        }

        var metadata = await dbContext.StoredFiles.AsNoTracking().SingleAsync();
        Assert.Equal(ownerId, metadata.OwnerUserId);
        Assert.Equal(stored.Sha256, metadata.Sha256);
        Assert.Equal(StoredFileOrigin.ManualUpload, metadata.Origin);
        Assert.Equal(StoredFileRecordType.SourceDocument, metadata.RecordType);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), metadata.RecordId);
        Assert.Equal(1, metadata.RecordVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"firemka-files-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
