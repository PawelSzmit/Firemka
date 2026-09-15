using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Firemka.Application.Security;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Security;
using Firemka.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

public sealed class AuthenticationFlowTests : IClassFixture<FiremkaWebApplicationFactory>
{
    private readonly FiremkaWebApplicationFactory _factory;

    public AuthenticationFlowTests(FiremkaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_setup_requires_totp_before_password_login_can_finish()
    {
        await _factory.ResetDatabaseAsync();
        using var enrollmentClient = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, enrollmentClient);

        using var client = WebTestSupport.CreateCookieClient(_factory);
        using var passwordResponse = await WebTestSupport.StartPasswordLoginAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, passwordResponse.StatusCode);
        Assert.Equal("/Account/TwoFactor", WebTestSupport.GetLocationPath(passwordResponse));
        Assert.DoesNotContain(
            passwordResponse.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith("Firemka.Session=", StringComparison.Ordinal));

        var key = await WebTestSupport.GetAuthenticatorKeyAsync(_factory);
        using var twoFactorResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Account/TwoFactor",
            new Dictionary<string, string>
            {
                ["Input.Code"] = WebTestSupport.CreateTotpCode(key),
                ["Input.TrustThisDevice"] = "false",
                ["Input.UseRecoveryCode"] = "false",
            });

        Assert.Equal(HttpStatusCode.Redirect, twoFactorResponse.StatusCode);
        Assert.Equal("/", WebTestSupport.GetLocationPath(twoFactorResponse));
        Assert.Contains(
            twoFactorResponse.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith("Firemka.Session=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recovery_code_logs_in_once_and_cannot_be_reused()
    {
        await _factory.ResetDatabaseAsync();
        using var enrollmentClient = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, enrollmentClient);
        var recoveryCode = await WebTestSupport.GenerateRecoveryCodeAsync(_factory);

        using var firstClient = WebTestSupport.CreateCookieClient(_factory);
        using var passwordResponse = await WebTestSupport.StartPasswordLoginAsync(firstClient);
        Assert.Equal("/Account/TwoFactor", WebTestSupport.GetLocationPath(passwordResponse));

        using var firstRecoveryResponse = await WebTestSupport.PostFormAsync(
            firstClient,
            "/Account/TwoFactor",
            new Dictionary<string, string>
            {
                ["Input.Code"] = recoveryCode,
                ["Input.UseRecoveryCode"] = "true",
                ["Input.TrustThisDevice"] = "false",
            });
        Assert.Equal(HttpStatusCode.Redirect, firstRecoveryResponse.StatusCode);
        Assert.Equal("/", WebTestSupport.GetLocationPath(firstRecoveryResponse));

        using var secondClient = WebTestSupport.CreateCookieClient(_factory);
        using var secondPasswordResponse = await WebTestSupport.StartPasswordLoginAsync(secondClient);
        Assert.Equal("/Account/TwoFactor", WebTestSupport.GetLocationPath(secondPasswordResponse));

        using var reusedRecoveryResponse = await WebTestSupport.PostFormAsync(
            secondClient,
            "/Account/TwoFactor",
            new Dictionary<string, string>
            {
                ["Input.Code"] = recoveryCode,
                ["Input.UseRecoveryCode"] = "true",
                ["Input.TrustThisDevice"] = "false",
            });
        Assert.Equal(HttpStatusCode.OK, reusedRecoveryResponse.StatusCode);
        Assert.False(
            reusedRecoveryResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders)
            && setCookieHeaders.Any(header => header.StartsWith("Firemka.Session=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Trusted_device_can_bypass_totp_then_is_invalid_after_revoke_or_expiry()
    {
        await _factory.ResetDatabaseAsync();
        using var enrollmentClient = WebTestSupport.CreateCookieClient(_factory);
        await WebTestSupport.EnrollOwnerAsync(_factory, enrollmentClient);
        var owner = await WebTestSupport.GetOwnerAsync(_factory);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var trustedDeviceService = scope.ServiceProvider.GetRequiredService<ITrustedDeviceService>();
            var issuedDevice = await trustedDeviceService.IssueAsync(owner.Id, "Test device");

            Assert.True(await trustedDeviceService.ValidateAndTouchAsync(owner.Id, issuedDevice.Token));
            await trustedDeviceService.RevokeAllAsync(owner.Id);
            Assert.False(await trustedDeviceService.ValidateAndTouchAsync(owner.Id, issuedDevice.Token));
        }

        var activeDevice = await IssueDeviceAsync(owner.Id);
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using var loginPage = await client.GetAsync("/Account/Login");
        var antiforgeryToken = WebTestSupport.ExtractAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
        var antiforgeryCookie = WebTestSupport.ExtractCookiePair(loginPage);

        using var trustedLoginRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = WebTestSupport.OwnerPassword,
                ["__RequestVerificationToken"] = antiforgeryToken,
            }),
        };
        trustedLoginRequest.Headers.TryAddWithoutValidation(
            "Cookie",
            $"{antiforgeryCookie}; {TrustedDeviceCookie.Name}={activeDevice.Token}");

        using var trustedLoginResponse = await client.SendAsync(trustedLoginRequest);
        Assert.Equal(HttpStatusCode.Redirect, trustedLoginResponse.StatusCode);
        Assert.Equal("/", WebTestSupport.GetLocationPath(trustedLoginResponse));

        var expiredDevice = await AddExpiredDeviceAsync(owner.Id);
        await using var validationScope = _factory.Services.CreateAsyncScope();
        var validationService = validationScope.ServiceProvider.GetRequiredService<ITrustedDeviceService>();
        Assert.False(await validationService.ValidateAndTouchAsync(owner.Id, expiredDevice.Token));
    }

    [Fact]
    public async Task Anonymous_post_without_antiforgery_token_is_rejected_and_protected_pages_stay_private()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        using var csrfResponse = await client.PostAsync(
            "/Setup",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = WebTestSupport.OwnerPassword,
                ["Input.ConfirmPassword"] = WebTestSupport.OwnerPassword,
            }));
        using var privatePageResponse = await client.GetAsync("/Invoices/Index");
        using var settingsResponse = await client.GetAsync("/Settings/Devices");

        Assert.Equal(HttpStatusCode.BadRequest, csrfResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, privatePageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, settingsResponse.StatusCode);
    }

    [Fact]
    public async Task Antiforgery_cookie_is_secure_outside_development()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        using var response = await client.GetAsync("/Setup");
        var antiforgeryCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));

        Assert.Contains("; secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Totp_setup_embeds_a_local_png_qr_and_keeps_the_text_key()
    {
        await _factory.ResetDatabaseAsync();
        using var client = WebTestSupport.CreateCookieClient(_factory);
        using var setupResponse = await WebTestSupport.PostFormAsync(
            client,
            "/Setup",
            new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = WebTestSupport.OwnerPassword,
                ["Input.ConfirmPassword"] = WebTestSupport.OwnerPassword,
            });
        Assert.Equal(HttpStatusCode.Redirect, setupResponse.StatusCode);

        using var response = await client.GetAsync("/Setup/TwoFactor");
        var html = await response.Content.ReadAsStringAsync();
        var qrMatch = Regex.Match(
            html,
            "src=\"data:image/png;base64,(?<image>[^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(qrMatch.Success, "Strona konfiguracji TOTP nie zawiera lokalnego obrazu PNG z kodem QR.");
        Assert.Equal(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            Convert.FromBase64String(WebUtility.HtmlDecode(qrMatch.Groups["image"].Value))[..8]);
        Assert.Contains("Klucz konfiguracji", html, StringComparison.Ordinal);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resuming_owner_setup_audits_successful_and_failed_password_attempts()
    {
        await _factory.ResetDatabaseAsync();
        using var creationClient = WebTestSupport.CreateCookieClient(_factory);
        using var ownerCreationResponse = await WebTestSupport.PostFormAsync(
            creationClient,
            "/Setup",
            new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = WebTestSupport.OwnerPassword,
                ["Input.ConfirmPassword"] = WebTestSupport.OwnerPassword,
            });
        Assert.Equal(HttpStatusCode.Redirect, ownerCreationResponse.StatusCode);

        using var failedResumeClient = WebTestSupport.CreateCookieClient(_factory);
        using var failedResumeResponse = await WebTestSupport.PostFormAsync(
            failedResumeClient,
            "/Setup/Resume",
            new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = "Wrong-Password-1!",
            });
        Assert.Equal(HttpStatusCode.OK, failedResumeResponse.StatusCode);

        using var successfulResumeClient = WebTestSupport.CreateCookieClient(_factory);
        using var successfulResumeResponse = await WebTestSupport.PostFormAsync(
            successfulResumeClient,
            "/Setup/Resume",
            new Dictionary<string, string>
            {
                ["Input.Email"] = WebTestSupport.OwnerEmail,
                ["Input.Password"] = WebTestSupport.OwnerPassword,
            });
        Assert.Equal(HttpStatusCode.Redirect, successfulResumeResponse.StatusCode);
        Assert.Equal("/Setup/TwoFactor", WebTestSupport.GetLocationPath(successfulResumeResponse));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditEvents = await dbContext.LoginAuditEvents
            .AsNoTracking()
            .Where(item => item.Method == "setup-resume-password")
            .ToListAsync();

        Assert.Contains(auditEvents, item => item.Outcome == "Failed");
        Assert.Contains(auditEvents, item => item.Outcome == "Successful");
    }

    private async Task<TrustedDeviceIssue> IssueDeviceAsync(string userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var trustedDeviceService = scope.ServiceProvider.GetRequiredService<ITrustedDeviceService>();
        return await trustedDeviceService.IssueAsync(userId, "Trusted login device");
    }

    private async Task<TrustedDeviceIssue> AddExpiredDeviceAsync(string userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.TrustedDevices.Add(new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            DisplayName = "Expired test device",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-31),
            LastUsedAtUtc = DateTimeOffset.UtcNow.AddDays(-31),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await dbContext.SaveChangesAsync();
        return new TrustedDeviceIssue(token, DateTimeOffset.UtcNow.AddDays(-1));
    }
}
