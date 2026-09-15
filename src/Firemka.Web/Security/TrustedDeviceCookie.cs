using Firemka.Application.Security;

namespace Firemka.Web.Security;

public static class TrustedDeviceCookie
{
    public const string Name = "Firemka.TrustedDevice";

    public static string? TryRead(HttpRequest request)
    {
        return request.Cookies.TryGetValue(Name, out var token) ? token : null;
    }

    public static void Write(HttpResponse response, HttpRequest request, TrustedDeviceIssue issue)
    {
        response.Cookies.Append(Name, issue.Token, BuildCookieOptions(request, issue.ExpiresAtUtc));
    }

    public static void Delete(HttpResponse response, HttpRequest request)
    {
        response.Cookies.Delete(Name, BuildCookieOptions(request, DateTimeOffset.UnixEpoch));
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
