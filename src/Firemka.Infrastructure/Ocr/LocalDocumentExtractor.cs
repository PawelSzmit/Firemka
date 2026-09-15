using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Firemka.Application.ExternalServices;
using Firemka.Domain.Documents;

namespace Firemka.Infrastructure.Ocr;

public sealed partial class LocalDocumentExtractor(IDocumentTextExtractionEngine textEngine)
    : IDocumentExtractor
{
    public async Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = await textEngine.ExtractTextAsync(
            request.FileName,
            request.MediaType,
            request.Content,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new DocumentExtractionResult(
                "NeedsManualReview",
                JsonSerializer.Serialize(DocumentData.Empty),
                null);
        }

        var invoiceNumber = MatchValue(InvoiceNumberRegex(), text);
        var sellerName = MatchValue(SellerRegex(), text);
        var sellerTaxId = MatchValue(NipRegex(), text)?.Replace("-", string.Empty, StringComparison.Ordinal);
        var dateText = MatchValue(IssueDateRegex(), text);
        var amountText = MatchValue(GrossAmountRegex(), text);
        var currency = MatchValue(CurrencyRegex(), text)?.ToUpperInvariant();

        DateOnly? issueDate = DateOnly.TryParseExact(
            dateText,
            ["dd.MM.yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate
            : null;
        decimal? grossAmount = TryParseAmount(amountText, out var parsedAmount)
            ? parsedAmount
            : null;
        var data = new DocumentData(
            invoiceNumber,
            sellerName,
            sellerTaxId,
            issueDate,
            grossAmount,
            currency);
        var found = new object?[] { invoiceNumber, sellerName, issueDate, grossAmount, currency }
            .Count(value => value is not null);
        var confidence = Math.Min(0.95m, 0.45m + (found * 0.1m));
        var fieldConfidences = new Dictionary<string, decimal>();
        AddConfidence(fieldConfidences, nameof(DocumentData.InvoiceNumber), invoiceNumber, 0.82m);
        AddConfidence(fieldConfidences, nameof(DocumentData.SellerName), sellerName, 0.72m);
        AddConfidence(fieldConfidences, nameof(DocumentData.SellerTaxId), sellerTaxId, 0.88m);
        AddConfidence(fieldConfidences, nameof(DocumentData.IssueDate), issueDate, 0.9m);
        AddConfidence(fieldConfidences, nameof(DocumentData.GrossAmount), grossAmount, 0.84m);
        AddConfidence(fieldConfidences, nameof(DocumentData.Currency), currency, 0.9m);

        return new DocumentExtractionResult(
            found == 0 ? "NeedsManualReview" : "Extracted",
            JsonSerializer.Serialize(data),
            found == 0 ? null : confidence,
            JsonSerializer.Serialize(fieldConfidences));
    }

    private static string? MatchValue(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static bool TryParseAmount(string? input, out decimal value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static void AddConfidence(
        IDictionary<string, decimal> target,
        string field,
        object? value,
        decimal confidence)
    {
        if (value is not null)
        {
            target[field] = confidence;
        }
    }

    [GeneratedRegex(@"(?:Faktura(?:\s+VAT)?(?:\s+nr)?|Nr\s+faktury)\s*:\s*([A-Za-z0-9/_\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceNumberRegex();

    [GeneratedRegex(@"Sprzedawca\s*:\s*([^\r\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SellerRegex();

    [GeneratedRegex(@"NIP\s*:\s*([0-9\-]{10,13})", RegexOptions.IgnoreCase)]
    private static partial Regex NipRegex();

    [GeneratedRegex(@"Data\s+wystawienia\s*:\s*([0-9]{2}\.[0-9]{2}\.[0-9]{4}|[0-9]{4}-[0-9]{2}-[0-9]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex IssueDateRegex();

    [GeneratedRegex(@"Razem\s+brutto\s*:\s*([0-9][0-9 ,.]*[0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex GrossAmountRegex();

    [GeneratedRegex(@"Razem\s+brutto\s*:\s*[0-9][0-9 ,.]*[0-9]\s*([A-Za-z]{3})", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyRegex();
}
