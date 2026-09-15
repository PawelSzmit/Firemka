using System.Net;
using Firemka.Domain.Companies;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class Phase10PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase10PagesTests(FiremkaWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/Payments?year=2026&month=9")]
    [InlineData("/Month?year=2026&month=9")]
    public async Task Payment_pages_require_login(string path)
    {
        using var client = WebTestSupport.CreateCookieClient(_factory);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(response));
    }

    [Fact]
    public async Task Payments_and_dashboard_explain_full_payments_and_backup_state()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(Company.Register(
                owner.Id, "Firma Płatności", "1010000000", "Adres firmy", new DateOnly(2026, 9, 1),
                "Klient", "1234567890", "Adres klienta", "Usługa", 1_000m, 23m, 1m,
                VehicleArrangement.None, DateTimeOffset.Parse("2026-09-14T08:00:00Z")));
            await db.SaveChangesAsync();
        }

        var payments = WebUtility.HtmlDecode(await client.GetStringAsync("/Payments?year=2026&month=9"));
        Assert.Contains("zapisuje tylko pełne płatności", payments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Historia zapisanych płatności", payments, StringComparison.Ordinal);
        Assert.Contains("Nie zapisano jeszcze żadnej płatności", payments, StringComparison.Ordinal);

        var dashboard = WebUtility.HtmlDecode(await client.GetStringAsync("/Month?year=2026&month=9"));
        Assert.Contains("Płatności", dashboard, StringComparison.Ordinal);
        Assert.Contains("Kopie nie są jeszcze skonfigurowane", dashboard, StringComparison.Ordinal);
        Assert.Contains("/Payments?year=2026&month=9", dashboard, StringComparison.Ordinal);
    }
}
