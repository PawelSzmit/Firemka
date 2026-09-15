using System.Net;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Documents;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class IncomingDocumentPagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public IncomingDocumentPagesTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_can_upload_a_scan_and_missing_fields_are_explained_before_confirmation()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);

        using var page = await client.GetAsync("/Invoices/Incoming");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Dodaj dokument", html, StringComparison.Ordinal);
        var token = WebTestSupport.ExtractAntiforgeryToken(html);
        using var uploadForm = new MultipartFormDataContent();
        uploadForm.Add(new StringContent(token), "__RequestVerificationToken");
        var bytes = new ByteArrayContent(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x01 });
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadForm.Add(bytes, "Upload", "scan.png");

        using var uploadResponse = await client.PostAsync("/Invoices/Incoming?handler=Upload", uploadForm);

        Assert.Equal(HttpStatusCode.Redirect, uploadResponse.StatusCode);
        var location = uploadResponse.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.StartsWith("/Invoices/Incoming/", location, StringComparison.Ordinal);

        Guid documentId;
        Guid sourceFileId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = await db.SourceDocuments.SingleAsync();
            document.ApplyExtraction(DocumentData.Empty, null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            documentId = document.Id;
            sourceFileId = document.SourceFileId!.Value;
        }

        using var preview = await client.GetAsync($"/Files/{sourceFileId}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("SAMEORIGIN", Assert.Single(preview.Headers.GetValues("X-Frame-Options")));
        Assert.Contains("frame-ancestors 'self'", Assert.Single(preview.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);

        using var detailsPage = await client.GetAsync($"/Invoices/Incoming/{documentId}");
        Assert.Equal(HttpStatusCode.OK, detailsPage.StatusCode);
        var detailsHtml = await detailsPage.Content.ReadAsStringAsync();
        var detailsToken = WebTestSupport.ExtractAntiforgeryToken(detailsHtml);
        using var invalidResponse = await client.PostAsync(
            $"/Invoices/Incoming/{documentId}?handler=Confirm",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = detailsToken,
                ["Input.InvoiceNumber"] = string.Empty,
                ["Input.SellerName"] = string.Empty,
                ["Input.SellerTaxId"] = string.Empty,
                ["Input.IssueDate"] = string.Empty,
                ["Input.GrossAmount"] = string.Empty,
                ["Input.Currency"] = string.Empty,
            }));

        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Contains("numer faktury", invalidHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kwot", invalidHtml, StringComparison.OrdinalIgnoreCase);

        var validToken = WebTestSupport.ExtractAntiforgeryToken(invalidHtml);
        using var validResponse = await client.PostAsync(
            $"/Invoices/Incoming/{documentId}?handler=Confirm",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = validToken,
                ["Input.InvoiceNumber"] = "FV/10/2026",
                ["Input.SellerName"] = "Sztuczny Dostawca",
                ["Input.SellerTaxId"] = "1234567890",
                ["Input.IssueDate"] = "2026-09-10",
                ["Input.GrossAmount"] = "199.99",
                ["Input.Currency"] = "PLN",
            }));

        Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(
            SourceDocumentStatus.RuleToDefine,
            (await verificationDb.SourceDocuments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Missing_field_confidence_is_not_replaced_with_the_overall_confidence()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await UploadSyntheticPngAsync(client);

        Guid documentId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = await db.SourceDocuments.SingleAsync();
            document.ApplyExtraction(
                new DocumentData(
                    "FV/10/2026",
                    "Sztuczny Dostawca",
                    null,
                    new DateOnly(2026, 9, 10),
                    199.99m,
                    null),
                0.91m,
                DateTimeOffset.UtcNow);
            db.DocumentExtractionAttempts.Add(new DocumentExtractionAttempt
            {
                Id = Guid.NewGuid(),
                SourceDocumentId = document.Id,
                StoredFileId = document.SourceFileId!.Value,
                AttemptNumber = 1,
                Status = DocumentExtractionAttemptStatus.Completed,
                Engine = "synthetic-test",
                FieldsJson = "{}",
                Confidence = 0.91m,
                FieldConfidencesJson = "{\"InvoiceNumber\":0.82}",
                StartedAtUtc = DateTimeOffset.UtcNow,
                FinishedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var html = await client.GetStringAsync($"/Invoices/Incoming/{documentId}");

        Assert.Contains("Pewność podpowiedzi", html, StringComparison.Ordinal);
        Assert.Contains("91%", html, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "Pewność: 82%").Cast<System.Text.RegularExpressions.Match>());
        Assert.Collection(
            System.Text.RegularExpressions.Regex.Matches(html, "Pewność: brak podpowiedzi").Cast<System.Text.RegularExpressions.Match>(),
            _ => { },
            _ => { },
            _ => { },
            _ => { },
            _ => { });
    }

    [Fact]
    public async Task Source_conflict_is_visible_even_when_the_document_was_already_booked()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await UploadSyntheticPngAsync(client);

        Guid documentId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var document = await db.SourceDocuments.SingleAsync();
            document.ApplyExtraction(
                new DocumentData(
                    "FV/10/2026",
                    "Sztuczny Dostawca",
                    "1234567890",
                    new DateOnly(2026, 9, 10),
                    199.99m,
                    "PLN"),
                1m,
                DateTimeOffset.UtcNow);
            document.TransitionTo(SourceDocumentStatus.Booked, DateTimeOffset.UtcNow);
            document.MarkSourceConflict(DateTimeOffset.UtcNow);
            db.Entry(document.SourceConflicts.Single()).State = EntityState.Added;
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        var detailsHtml = await client.GetStringAsync($"/Invoices/Incoming/{documentId}");
        var listHtml = await client.GetStringAsync("/Invoices/Incoming");

        Assert.Contains("inną treść dla tego samego numeru", detailsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Różniące się źródło", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("Konflikt źródła KSeF", listHtml, StringComparison.Ordinal);

        var token = WebTestSupport.ExtractAntiforgeryToken(detailsHtml);
        using var invalid = await client.PostAsync(
            $"/Invoices/Incoming/{documentId}?handler=ResolveConflict",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["SourceConflictResolution"] = string.Empty,
            }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Wyjaśnij", invalidHtml, StringComparison.OrdinalIgnoreCase);

        using var resolvedResponse = await client.PostAsync(
            $"/Invoices/Incoming/{documentId}?handler=ResolveConflict",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebTestSupport.ExtractAntiforgeryToken(invalidHtml),
                ["SourceConflictResolution"] = "Porównano źródła i zachowany dokument jest prawidłowy.",
            }));
        Assert.Equal(HttpStatusCode.Redirect, resolvedResponse.StatusCode);

        var resolvedDetails = await client.GetStringAsync($"/Invoices/Incoming/{documentId}");
        var resolvedList = await client.GetStringAsync("/Invoices/Incoming");
        Assert.Contains("Konflikt źródła został wyjaśniony", resolvedDetails, StringComparison.Ordinal);
        Assert.Contains("Historia konfliktów", resolvedDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("alert alert-danger", resolvedDetails, StringComparison.Ordinal);
        Assert.Contains("Konflikt wyjaśniony", resolvedList, StringComparison.Ordinal);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolvedDocument = await verificationDb.SourceDocuments.AsNoTracking().SingleAsync();
        Assert.Equal(SourceDocumentStatus.Booked, resolvedDocument.Status);
        Assert.True(resolvedDocument.HasSourceConflict);
        Assert.False(resolvedDocument.HasUnresolvedSourceConflict);
        Assert.Equal(
            "Porównano źródła i zachowany dokument jest prawidłowy.",
            resolvedDocument.SourceConflictResolution);
    }

    private static async Task UploadSyntheticPngAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Invoices/Incoming");
        using var form = new MultipartFormDataContent();
        form.Add(
            new StringContent(WebTestSupport.ExtractAntiforgeryToken(html)),
            "__RequestVerificationToken");
        var bytes = new ByteArrayContent(
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x01 });
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(bytes, "Upload", "scan.png");
        using var response = await client.PostAsync("/Invoices/Incoming?handler=Upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
