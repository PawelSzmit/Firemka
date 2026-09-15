namespace Firemka.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        var isPrivateFilePreview = context.Request.Path.StartsWithSegments("/Files");
        headers.TryAdd(
            "Content-Security-Policy",
            isPrivateFilePreview
                ? "default-src 'none'; frame-ancestors 'self'; sandbox"
                : "default-src 'self'; script-src 'self'; img-src 'self' data:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'");
        headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", isPrivateFilePreview ? "SAMEORIGIN" : "DENY");

        await next(context);
    }
}
