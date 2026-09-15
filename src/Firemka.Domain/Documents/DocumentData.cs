namespace Firemka.Domain.Documents;

public sealed record DocumentData(
    string? InvoiceNumber,
    string? SellerName,
    string? SellerTaxId,
    DateOnly? IssueDate,
    decimal? GrossAmount,
    string? Currency,
    string? SellerAddress = null)
{
    public static DocumentData Empty { get; } = new(null, null, null, null, null, null, null);
}

public sealed class DocumentDataValidationException(IReadOnlyList<string> errors)
    : InvalidOperationException($"Uzupełnij dane dokumentu: {string.Join(", ", errors)}.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
