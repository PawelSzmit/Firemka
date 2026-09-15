namespace Firemka.Infrastructure.Ocr;

public interface IDocumentTextExtractionEngine
{
    Task<string> ExtractTextAsync(
        string fileName,
        string mediaType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);
}
