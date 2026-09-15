using System.Net;
using System.Text;
using Firemka.Application.ExternalServices;
using Firemka.Infrastructure.Ksef.Incoming;

namespace Firemka.Infrastructure.Tests;

public sealed class KsefIncomingHttpGatewayTests
{
    [Fact]
    public async Task Metadata_query_uses_incoming_subject_and_the_configured_test_endpoint()
    {
        HttpRequestMessage? captured = null;
        string? requestBody = null;
        var handler = new DelegateHandler(async request =>
        {
            captured = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "hasMore": false,
                      "isTruncated": false,
                      "permanentStorageHwmDate": "2026-09-10T12:00:00Z",
                      "invoices": [{
                        "ksefNumber": "1234567890-20260910-TEST-01",
                        "invoiceNumber": "FV/1/2026",
                        "issueDate": "2026-09-09",
                        "permanentStorageDate": "2026-09-10T10:00:00Z",
                        "seller": { "nip": "1234567890", "name": "Sztuczny Dostawca" },
                        "grossAmount": 123.45,
                        "currency": "PLN"
                      }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var options = TestOptions();
        using var client = new HttpClient(handler) { BaseAddress = options.GetBaseUri() };
        var gateway = new KsefIncomingHttpGateway(client, options);

        var page = await gateway.GetIncomingPageAsync(new KsefInvoiceQuery(
            KsefEnvironment.Test,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            null));

        Assert.NotNull(captured);
        Assert.Equal("api-test.ksef.mf.gov.pl", captured.RequestUri!.Host);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("synthetic-test-token", captured.Headers.Authorization.Parameter);
        Assert.Contains("\"subjectType\":\"Subject2\"", requestBody, StringComparison.Ordinal);
        Assert.Single(page.Invoices);
        Assert.Equal("FV/1/2026", page.Invoices[0].InvoiceNumber);
    }

    [Fact]
    public async Task Download_rejects_xml_when_the_official_hash_header_does_not_match()
    {
        var handler = new DelegateHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("<Invoice>synthetic</Invoice>"u8.ToArray()),
            Headers = { { "x-ms-meta-hash", Convert.ToBase64String(new byte[32]) } },
        }));
        var options = TestOptions();
        using var client = new HttpClient(handler) { BaseAddress = options.GetBaseUri() };
        var gateway = new KsefIncomingHttpGateway(client, options);
        var metadata = new KsefInvoiceMetadata(
            "1234567890-20260910-TEST-01",
            "FV/1/2026",
            new DateOnly(2026, 9, 9),
            DateTimeOffset.Parse("2026-09-10T10:00:00Z"),
            "Sztuczny Dostawca",
            "1234567890",
            123.45m,
            "PLN",
            ReadOnlyMemory<byte>.Empty);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => gateway.DownloadAsync(metadata));

        Assert.Contains("skrót", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expired_access_is_reported_without_exposing_the_token()
    {
        var handler = new DelegateHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var options = TestOptions();
        using var client = new HttpClient(handler) { BaseAddress = options.GetBaseUri() };
        var gateway = new KsefIncomingHttpGateway(client, options);

        var exception = await Assert.ThrowsAsync<KsefAuthorizationException>(() =>
            gateway.GetIncomingPageAsync(new KsefInvoiceQuery(
                KsefEnvironment.Test,
                DateTimeOffset.UtcNow.AddDays(-1),
                null)));

        Assert.Contains("odśwież", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(options.AccessToken!, exception.Message, StringComparison.Ordinal);
    }

    private static KsefIncomingOptions TestOptions() => new()
    {
        Enabled = true,
        Environment = KsefEnvironment.Test,
        AccessToken = "synthetic-test-token",
    };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
