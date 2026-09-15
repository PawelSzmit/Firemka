using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Firemka.Web.Tests;

public sealed class RateLimitTests
{
    [Fact]
    public async Task Setup_page_views_do_not_consume_the_authentication_attempt_limit()
    {
        using var factory = FiremkaWebApplicationFactory.ForEnvironment("RateLimitTesting");
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        for (var pageView = 0; pageView < 11; pageView++)
        {
            using var response = await client.GetAsync("/Setup");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Setup_assets_are_available_before_the_owner_signs_in()
    {
        using var factory = FiremkaWebApplicationFactory.ForEnvironment("RateLimitTesting");
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        var assetPaths = new[]
        {
            "/lib/bootstrap/dist/css/bootstrap.min.css",
            "/css/site.css",
            "/lib/jquery/dist/jquery.min.js",
            "/lib/bootstrap/dist/js/bootstrap.bundle.min.js",
            "/js/site.js",
        };

        foreach (var assetPath in assetPaths)
        {
            using var response = await client.GetAsync(assetPath);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{assetPath} returned {(int)response.StatusCode} {response.StatusCode} " +
                $"and redirected to {response.Headers.Location}");
        }
    }

    [Fact]
    public async Task Authentication_posts_reject_the_eleventh_attempt_with_a_browser_readable_message()
    {
        using var factory = FiremkaWebApplicationFactory.ForEnvironment("RateLimitTesting");
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        using var pageResponse = await client.GetAsync("/Setup");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var antiforgeryToken = WebTestSupport.ExtractAntiforgeryToken(
            await pageResponse.Content.ReadAsStringAsync());

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await PostInvalidSetupAttemptAsync(client, antiforgeryToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var rejectedResponse = await PostInvalidSetupAttemptAsync(client, antiforgeryToken);
        Assert.Equal((HttpStatusCode)429, rejectedResponse.StatusCode);
        Assert.Equal("text/plain", rejectedResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Spróbuj ponownie", await rejectedResponse.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostInvalidSetupAttemptAsync(
        HttpClient client,
        string antiforgeryToken)
    {
        return client.PostAsync(
            "/Setup",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "not-an-email",
                ["Input.Password"] = "Invalid-Password-1!",
                ["Input.ConfirmPassword"] = "Invalid-Password-1!",
                ["__RequestVerificationToken"] = antiforgeryToken,
            }));
    }
}
