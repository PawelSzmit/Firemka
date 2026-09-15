using System.Security.Claims;

namespace Firemka.Web.Security;

public static class RequestIdentity
{
    public static string GetRequiredUserId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Brakuje identyfikatora zalogowanego użytkownika.");
    }

    public static string? GetIpAddress(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString();
    }

    public static string GetDeviceName(HttpRequest request)
    {
        const int maximumLength = 120;
        var userAgent = request.Headers.UserAgent.ToString().Trim();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "Nieznane urządzenie";
        }

        return userAgent.Length <= maximumLength ? userAgent : userAgent[..maximumLength];
    }
}
