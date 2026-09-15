namespace Firemka.Infrastructure.Files;

public sealed class PrivateFileStoreOptions
{
    public const string SectionName = "PrivateFileStore";

    public string RootPath { get; set; } = "/var/lib/firemka/files";

    public long MaximumFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
