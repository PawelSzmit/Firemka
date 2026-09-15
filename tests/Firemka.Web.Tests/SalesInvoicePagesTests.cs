using System.Net;
using Firemka.Domain.Companies;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class SalesInvoicePagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public SalesInvoicePagesTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_can_prepare_and_edit_a_draft_but_cannot_send_while_test_ksef_is_disabled()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        await SeedCompanyAsync();

        var listHtml = await client.GetStringAsync("/Invoices/Sales");
        Assert.Contains("Przygotuj wersję roboczą", listHtml, StringComparison.Ordinal);
        Assert.Contains("wysyłka do KSeF jest wyłączona", listHtml, StringComparison.OrdinalIgnoreCase);
        using var createResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Invoices/Sales?handler=Prepare",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        Guid invoiceId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.SalesInvoices.SingleAsync();
            invoiceId = invoice.Id;
            Assert.Equal(SalesInvoiceStatus.Draft, invoice.Status);
            Assert.Equal(1_500m, invoice.NetAmount);
        }

        var detailsPath = $"/Invoices/Sales/{invoiceId}";
        var detailsHtml = await client.GetStringAsync(detailsPath);
        var normalizedDetailsHtml = detailsHtml
            .Replace("&nbsp;", " ", StringComparison.Ordinal)
            .Replace('\u00a0', ' ')
            .Replace('\u202f', ' ');
        Assert.Contains("Pełny miesiąc", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("1 500,00 zł", normalizedDetailsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("1,500.00", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("disabled", detailsHtml, StringComparison.OrdinalIgnoreCase);
        using var editResponse = await WebTestSupport.PostFormAsync(
            client,
            $"{detailsPath}?handler=Edit",
            new Dictionary<string, string>
            {
                ["Input.NetAmount"] = "1650,50",
                ["Input.VatRate"] = "23,00",
            });
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var edited = await verificationDb.SalesInvoices.AsNoTracking().SingleAsync();
        Assert.True(edited.HasManualAmountOverride);
        Assert.Equal(1_650.50m, edited.NetAmount);

        using var dotEditResponse = await WebTestSupport.PostFormAsync(
            client,
            $"{detailsPath}?handler=Edit",
            new Dictionary<string, string>
            {
                ["Input.NetAmount"] = "1750.75",
                ["Input.VatRate"] = "23.00",
            });
        Assert.Equal(HttpStatusCode.Redirect, dotEditResponse.StatusCode);

        await using var dotVerificationScope = _factory.Services.CreateAsyncScope();
        var dotVerificationDb = dotVerificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(
            1_750.75m,
            await dotVerificationDb.SalesInvoices.Select(item => item.NetAmount).SingleAsync());
    }

    private async Task SeedCompanyAsync()
    {
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(Company.Register(
            owner.Id,
            "Testowa Firma",
            "1234563218",
            "Testowa 2, 00-002 Warszawa",
            new DateOnly(2026, 9, 15),
            "Testowy Klient",
            "1234563218",
            "Testowa 1, 00-001 Warszawa",
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }
}
