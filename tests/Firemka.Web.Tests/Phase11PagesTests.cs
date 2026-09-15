using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Firemka.Application.Backups;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed partial class Phase11PagesTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public Phase11PagesTests(FiremkaWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Backup_page_requires_login_and_api_requires_a_dedicated_token()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        using var page = await client.GetAsync("/Backups");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(page));
        using var api = await client.GetAsync("/api/backups/plan");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task Token_is_shown_once_hash_only_is_stored_and_report_updates_status_without_csrf_cookie()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, client);

        using var generate = await WebTestSupport.PostFormAsync(
            client, "/Backups?handler=GenerateToken", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        var html = WebUtility.HtmlDecode(await generate.Content.ReadAsStringAsync());
        var match = RawTokenRegex().Match(html);
        Assert.True(match.Success, "Nowy token kopii nie został pokazany.");
        var rawToken = match.Value;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.BackupAccessTokens.SingleAsync();
            Assert.DoesNotContain(rawToken, stored.SecretSha256, StringComparison.Ordinal);
        }

        using var refresh = await client.GetAsync("/Backups");
        Assert.DoesNotContain(rawToken, await refresh.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var apiClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        using var plan = await apiClient.GetAsync("/api/backups/plan");
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        Assert.True((await plan.Content.ReadFromJsonAsync<BackupPlan>())!.DailyRequired);

        using var requestNow = await WebTestSupport.PostFormAsync(
            client, "/Backups?handler=RequestNow", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, requestNow.StatusCode);
        var pendingStatus = WebUtility.HtmlDecode(await client.GetStringAsync("/Backups"));
        Assert.Contains("Kopia na żądanie oczekuje", pendingStatus, StringComparison.Ordinal);
        using var requestedPlanResponse = await apiClient.GetAsync("/api/backups/plan");
        var requestedPlan = await requestedPlanResponse.Content.ReadFromJsonAsync<BackupPlan>();
        Assert.NotNull(requestedPlan?.ManualRequestId);

        using var report = await apiClient.PostAsJsonAsync("/api/backups/report", new BackupReportCommand(
            true, "firemka-backup-20260915T000000000Z-v1.fmbak", new string('A', 64), 1, null,
            requestedPlan.ManualRequestId, null));
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

        var status = WebUtility.HtmlDecode(await client.GetStringAsync("/Backups"));
        Assert.Contains("Ostatnia sprawdzona kopia", status, StringComparison.Ordinal);
        Assert.Contains("firemka-backup-20260915T000000000Z-v1.fmbak", status, StringComparison.Ordinal);
    }

    [GeneratedRegex("fmbk_[a-f0-9]{12}_[A-Za-z0-9_-]{40,50}", RegexOptions.CultureInvariant)]
    private static partial Regex RawTokenRegex();
}
