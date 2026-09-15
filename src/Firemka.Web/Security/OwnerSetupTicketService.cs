using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Firemka.Web.Security;

public sealed class OwnerSetupTicketService(IDataProtectionProvider dataProtectionProvider)
{
    private const string CookieName = "Firemka.OwnerSetup";
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(15);
    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("Firemka.OwnerSetup.v1")
        .ToTimeLimitedDataProtector();

    public void Issue(HttpResponse response, HttpRequest request, string userId)
    {
        var ticket = _protector.Protect(userId, TicketLifetime);
        response.Cookies.Append(CookieName, ticket, BuildCookieOptions(request, DateTimeOffset.UtcNow.Add(TicketLifetime)));
    }

    public string? TryRead(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var ticket) || string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(ticket, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Clear(HttpResponse response, HttpRequest request)
    {
        response.Cookies.Delete(CookieName, BuildCookieOptions(request, DateTimeOffset.UnixEpoch));
    }

    private static CookieOptions BuildCookieOptions(HttpRequest request, DateTimeOffset expires)
    {
        return new CookieOptions
        {
            Expires = expires,
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Strict,
            Secure = request.IsHttps,
        };
    }
}
