using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Firemka.Web.Tests;

public sealed class SecurityBoundaryTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public SecurityBoundaryTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Protected_start_page_redirects_anonymous_visitor_to_login()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", WebTestSupport.GetLocationPath(response));
    }

    [Fact]
    public async Task Public_registration_endpoint_is_not_exposed()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });

        using var response = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Content_security_policy_allows_local_scripts_and_bootstrap_data_images_without_inline_scripts()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var response = await client.GetAsync("/Account/Login");
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));

        Assert.Contains("script-src 'self'", policy);
        Assert.Contains("img-src 'self' data:", policy);
        Assert.DoesNotContain("'unsafe-inline'", policy);
    }
}
