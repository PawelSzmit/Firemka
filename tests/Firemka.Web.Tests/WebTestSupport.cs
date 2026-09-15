using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Firemka.Infrastructure.Identity;
using Firemka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Firemka.Web.Tests;

internal static partial class WebTestSupport
{
    public const string OwnerEmail = "owner@example.test";
    public const string OwnerPassword = "Very-Strong-Password-1!";

    public static HttpClient CreateCookieClient(FiremkaWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
    }

    public static async Task EnrollOwnerAsync(FiremkaWebApplicationFactory factory, HttpClient client)
    {
        var setupResponse = await PostFormAsync(
            client,
            "/Setup",
            new Dictionary<string, string>
            {
                ["Input.Email"] = OwnerEmail,
                ["Input.Password"] = OwnerPassword,
                ["Input.ConfirmPassword"] = OwnerPassword,
            });

        Assert.Equal(HttpStatusCode.Redirect, setupResponse.StatusCode);
        Assert.Equal("/Setup/TwoFactor", GetLocationPath(setupResponse));

        using var configurationPage = await client.GetAsync("/Setup/TwoFactor");
        Assert.Equal(HttpStatusCode.OK, configurationPage.StatusCode);
        var authenticatorKey = await GetAuthenticatorKeyAsync(factory);
        var twoFactorResponse = await PostFormAsync(
            client,
            "/Setup/TwoFactor",
            new Dictionary<string, string> { ["Input.Code"] = CreateTotpCode(authenticatorKey) });

        Assert.Equal(HttpStatusCode.Redirect, twoFactorResponse.StatusCode);
        Assert.Equal("/Setup/RecoveryCodes", GetLocationPath(twoFactorResponse));
    }

    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        IDictionary<string, string> fields)
    {
        using var pageResponse = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var antiforgeryToken = ExtractAntiforgeryToken(await pageResponse.Content.ReadAsStringAsync());

        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
        };

        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    public static async Task<string> GetAuthenticatorKeyAsync(FiremkaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(OwnerEmail);
        Assert.NotNull(user);
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        Assert.False(string.IsNullOrWhiteSpace(key));
        return key!;
    }

    public static async Task<string> GenerateRecoveryCodeAsync(FiremkaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(OwnerEmail);
        Assert.NotNull(user);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 1);
        return Assert.Single(recoveryCodes!);
    }

    public static async Task<ApplicationUser> GetOwnerAsync(FiremkaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(OwnerEmail);
        return Assert.IsType<ApplicationUser>(user);
    }

    public static async Task<HttpResponseMessage> StartPasswordLoginAsync(HttpClient client)
    {
        return await PostFormAsync(
            client,
            "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Email"] = OwnerEmail,
                ["Input.Password"] = OwnerPassword,
            });
    }

    public static string ExtractAntiforgeryToken(string html)
    {
        var match = AntiforgeryTokenRegex().Match(html);
        Assert.True(match.Success, "Nie znaleziono tokenu anty-CSRF w formularzu.");
        return WebUtility.HtmlDecode(match.Groups["token"].Value);
    }

    public static string? GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString?.Split('?', 2)[0];
    }

    public static string ExtractCookiePair(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(cookie));
        return cookie!.Split(';', 2)[0];
    }

    public static string CreateTotpCode(string base32Key)
    {
        var key = DecodeBase32(base32Key);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, timestep);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binaryCode = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binaryCode % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in value.Trim().TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new ArgumentException("Klucz TOTP ma nieprawidłowy format Base32.", nameof(value));
            }

            buffer = (buffer << 5) | index;
            bitsInBuffer += 5;
            if (bitsInBuffer < 8)
            {
                continue;
            }

            bitsInBuffer -= 8;
            bytes.Add((byte)(buffer >> bitsInBuffer));
            buffer &= (1 << bitsInBuffer) - 1;
        }

        return bytes.ToArray();
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTokenRegex();
}
