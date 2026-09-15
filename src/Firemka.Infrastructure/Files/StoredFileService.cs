using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Files;

public sealed class StoredFileService(
    AppDbContext dbContext,
    PrivateFileStore privateFileStore)
{
    public async Task<StoredFileReference> StoreAsync(
        string ownerUserId,
        PrivateFileUpload upload,
        StoredFileContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(context);
        if (context.RecordId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator powiązanego rekordu nie może być pusty.", nameof(context));
        }

        if (context.RecordVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "Wersja rekordu musi być dodatnia.");
        }

        if (!Enum.IsDefined(context.Origin) || !Enum.IsDefined(context.RecordType))
        {
            throw new ArgumentException("Kontekst pliku zawiera nieobsługiwaną wartość.", nameof(context));
        }

        var descriptor = await privateFileStore.SaveAsync(upload, cancellationToken);
        var storedFile = new StoredFile
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            StorageKey = descriptor.StorageKey,
            OriginalFileName = descriptor.OriginalFileName,
            MediaType = descriptor.MediaType,
            SizeBytes = descriptor.SizeBytes,
            Sha256 = descriptor.Sha256,
            Origin = context.Origin,
            RecordType = context.RecordType,
            RecordId = context.RecordId,
            RecordVersion = context.RecordVersion,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        dbContext.StoredFiles.Add(storedFile);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await privateFileStore.DeleteAsync(descriptor.StorageKey, CancellationToken.None);
            throw;
        }

        return new StoredFileReference(
            storedFile.Id,
            storedFile.OriginalFileName,
            storedFile.MediaType,
            storedFile.SizeBytes,
            storedFile.Sha256);
    }

    public async Task<StoredFileContent?> OpenAsync(
        Guid fileId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        var metadata = await dbContext.StoredFiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                file => file.Id == fileId && file.OwnerUserId == ownerUserId,
                cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        if (metadata.RecordType == StoredFileRecordType.FilingArtifactVersion)
        {
            var approved = await dbContext.FilingArtifacts.AsNoTracking().AnyAsync(
                artifact => artifact.StoredFileId == metadata.Id
                    && artifact.OwnerUserId == ownerUserId
                    && artifact.Status != Firemka.Domain.Filings.FilingArtifactStatus.ApprovalRequired,
                cancellationToken);
            if (!approved)
            {
                return null;
            }
        }

        var content = await privateFileStore.OpenReadAsync(metadata.StorageKey, cancellationToken);
        return new StoredFileContent(
            metadata.OriginalFileName,
            metadata.MediaType,
            metadata.SizeBytes,
            metadata.Sha256,
            content);
    }

    public async Task DeletePhysicalCopyAsync(
        Guid fileId,
        CancellationToken cancellationToken = default)
    {
        var storageKey = dbContext.StoredFiles.Local
            .FirstOrDefault(file => file.Id == fileId)?.StorageKey;
        storageKey ??= await dbContext.StoredFiles.AsNoTracking()
            .Where(file => file.Id == fileId)
            .Select(file => file.StorageKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            await privateFileStore.DeleteAsync(storageKey, cancellationToken);
        }
    }
}

public sealed record StoredFileReference(
    Guid Id,
    string OriginalFileName,
    string MediaType,
    long SizeBytes,
    string Sha256);

public sealed record StoredFileContent(
    string OriginalFileName,
    string MediaType,
    long SizeBytes,
    string Sha256,
    Stream Content);
