using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Firemka.Application.ExternalServices;

namespace Firemka.Infrastructure.Ksef.Incoming;

public sealed class KsefIncomingHttpGateway(
    HttpClient httpClient,
    KsefIncomingOptions options) : IIncomingKsefGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<KsefInvoicePage> GetIncomingPageAsync(
        KsefInvoiceQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(query.Environment);
        var cursor = ParseCursor(query.Cursor, query.InitialFromUtc);
        var nowUtc = DateTimeOffset.UtcNow;
        if (cursor.FromUtc > nowUtc)
        {
            return new KsefInvoicePage([], JsonSerializer.Serialize(cursor, JsonOptions), false);
        }

        var rangeEnd = cursor.FromUtc.AddDays(99) < nowUtc
            ? cursor.FromUtc.AddDays(99)
            : nowUtc;
        using var request = CreateRequest(
            HttpMethod.Post,
            $"invoices/query/metadata?sortOrder=Asc&pageOffset={cursor.PageOffset}&pageSize=100");
        request.Content = JsonContent.Create(new
        {
            subjectType = "Subject2",
            dateRange = new
            {
                dateType = "PermanentStorage",
                from = cursor.FromUtc,
                to = rangeEnd,
            },
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureKsefSuccess(response);
        var body = await response.Content.ReadFromJsonAsync<QueryResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("KSeF zwrócił pustą odpowiedź metadanych.");
        var invoices = body.Invoices.Select(item => new KsefInvoiceMetadata(
            item.KsefNumber,
            item.InvoiceNumber,
            item.IssueDate,
            item.PermanentStorageDate,
            item.Seller.Name,
            item.Seller.Nip,
            item.GrossAmount,
            item.Currency,
            ReadOnlyMemory<byte>.Empty)).ToArray();

        KsefApiCursor next;
        var hasMore = body.HasMore;
        if (body.HasMore && !body.IsTruncated)
        {
            next = cursor with { PageOffset = cursor.PageOffset + 1 };
        }
        else if (body.IsTruncated && invoices.Length > 0)
        {
            next = new KsefApiCursor(invoices[^1].PermanentStorageDateUtc, 0);
            hasMore = true;
        }
        else if (rangeEnd < nowUtc)
        {
            next = new KsefApiCursor(rangeEnd, 0);
            hasMore = true;
        }
        else
        {
            var highWaterMark = body.PermanentStorageHwmDate
                ?? invoices.LastOrDefault()?.PermanentStorageDateUtc
                ?? cursor.FromUtc;
            next = new KsefApiCursor(highWaterMark, 0);
        }

        return new KsefInvoicePage(
            invoices,
            JsonSerializer.Serialize(next, JsonOptions),
            hasMore);
    }

    public async Task<KsefDownloadedInvoice> DownloadAsync(
        KsefInvoiceMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        EnsureReady(options.Environment);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"invoices/ksef/{Uri.EscapeDataString(metadata.KsefNumber)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureKsefSuccess(response);
        var rawXml = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var hash = SHA256.HashData(rawXml);
        if (response.Headers.TryGetValues("x-ms-meta-hash", out var values))
        {
            var declared = values.SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(declared))
            {
                byte[] declaredHash;
                try
                {
                    declaredHash = Convert.FromBase64String(declared);
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException("KSeF zwrócił niepoprawny skrót dokumentu.", exception);
                }

                if (!CryptographicOperations.FixedTimeEquals(declaredHash, hash))
                {
                    throw new InvalidDataException("Skrót pobranego XML nie zgadza się z odpowiedzią KSeF.");
                }
            }
        }

        return new KsefDownloadedInvoice(metadata, rawXml, Convert.ToHexString(hash));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Error-Format", "problem-details");
        return request;
    }

    private void EnsureReady(KsefEnvironment requestedEnvironment)
    {
        options.ValidateForUse();
        if (requestedEnvironment != options.Environment)
        {
            throw new InvalidOperationException("Zapytanie wskazuje inne środowisko KSeF niż konfiguracja.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            throw new InvalidOperationException("Brakuje tokenu dostępu do KSeF w bezpiecznej konfiguracji.");
        }

        if (httpClient.BaseAddress != options.GetBaseUri())
        {
            throw new InvalidOperationException("Adres klienta KSeF nie zgadza się z wybranym środowiskiem.");
        }
    }

    private static void EnsureKsefSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new KsefAuthorizationException(
                "Dostęp do KSeF wygasł albo nie ma uprawnienia InvoiceRead. Odśwież testowy dostęp w bezpiecznej konfiguracji.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException(
                "KSeF ograniczył liczbę zapytań. Worker ponowi synchronizację z zapisanego kursora.",
                null,
                response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
    }

    private static KsefApiCursor ParseCursor(string? value, DateTimeOffset initialFromUtc)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new KsefApiCursor(initialFromUtc, 0);
        }

        return JsonSerializer.Deserialize<KsefApiCursor>(value, JsonOptions)
            ?? throw new InvalidDataException("Kursor synchronizacji KSeF jest niepoprawny.");
    }

    private sealed record KsefApiCursor(DateTimeOffset FromUtc, int PageOffset);

    private sealed record QueryResponse(
        bool HasMore,
        bool IsTruncated,
        DateTimeOffset? PermanentStorageHwmDate,
        IReadOnlyList<QueryInvoice> Invoices);

    private sealed record QueryInvoice(
        string KsefNumber,
        string InvoiceNumber,
        DateOnly IssueDate,
        DateTimeOffset PermanentStorageDate,
        QuerySeller Seller,
        decimal GrossAmount,
        string Currency);

    private sealed record QuerySeller(string? Nip, string Name);
}

public sealed class KsefAuthorizationException(string message) : InvalidOperationException(message);
