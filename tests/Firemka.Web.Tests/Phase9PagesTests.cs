using System.Net;
using Firemka.Domain.Companies;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class Phase9PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase9PagesTests(FiremkaWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Annual_closing_requires_login()
    {
        using var client = WebTestSupport.CreateCookieClient(_factory);
        using var response = await client.GetAsync("/Settlements/Year?year=2026");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(response));
    }

    [Fact]
    public async Task Annual_page_explains_scope_and_lists_actionable_blockers()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);
        var owner = await WebTestSupport.GetOwnerAsync(_factory);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(Company.Register(
                owner.Id, "Firma Roczna", "1010000000", "Adres firmy", new DateOnly(2026, 10, 1),
                "Klient", "1234567890", "Adres klienta", "Usługa", 1_000m, 23m, 1m,
                VehicleArrangement.None, DateTimeOffset.Parse("2026-09-12T08:00:00Z")));
            await db.SaveChangesAsync();
        }

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Settlements/Year?year=2026"));

        Assert.Contains("Firemka nie tworzy pełnego PIT-36 ani PIT/B", html, StringComparison.Ordinal);
        Assert.Contains("Rok można zamknąć dopiero po 31 grudnia", html, StringComparison.Ordinal);
        Assert.Contains("Uzupełnij i potwierdź dane roczne", html, StringComparison.Ordinal);
        Assert.Contains("month=10", html, StringComparison.Ordinal);
        Assert.Contains("Spis z natury na początek roku", html, StringComparison.Ordinal);
        Assert.Contains("Zaliczki PIT faktycznie wpłacone", html, StringComparison.Ordinal);
        Assert.Contains("Jak wysłać roczny JPK ręcznie", html, StringComparison.Ordinal);
        Assert.Contains("Firemka nie wysyła pliku automatycznie", html, StringComparison.Ordinal);
    }
}
