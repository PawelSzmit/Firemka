using System.Net.Http.Headers;
using Firemka.Application.Backups;

namespace Firemka.Web.Backups;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/backups")
            .AllowAnonymous()
            .RequireRateLimiting("backup-api");

        group.MapGet("/plan", async Task<IResult> (
            HttpContext context,
            IBackupService service,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAsync(context, service, timeProvider, cancellationToken);
            return authorization is null
                ? Results.Unauthorized()
                : Results.Json(await service.GetPlanAsync(
                    authorization, timeProvider.GetUtcNow(), cancellationToken));
        });

        group.MapGet("/export", async Task<IResult> (
            HttpContext context,
            IBackupService service,
            IBackupPayloadWriter writer,
            BackupExportGate gate,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAsync(context, service, timeProvider, cancellationToken);
            if (authorization is null) return Results.Unauthorized();
            var lease = await gate.TryEnterAsync(cancellationToken);
            if (lease is null) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentDisposition = "attachment; filename=firemka-payload.zip";
            return Results.Stream(async destination =>
            {
                using (lease)
                {
                    await writer.WriteAsync(
                        authorization.OwnerUserId,
                        destination,
                        timeProvider.GetUtcNow(),
                        context.RequestAborted);
                }
            }, "application/zip");
        });

        group.MapPost("/report", async Task<IResult> (
            HttpContext context,
            BackupReportCommand command,
            IBackupService service,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAsync(context, service, timeProvider, cancellationToken);
            if (authorization is null) return Results.Unauthorized();
            try
            {
                await service.ReportAsync(
                    authorization, command, timeProvider.GetUtcNow(), cancellationToken);
                return Results.NoContent();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<BackupAuthorization?> AuthorizeAsync(
        HttpContext context,
        IBackupService service,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
            return null;
        return await service.AuthorizeAsync(header.Parameter, timeProvider.GetUtcNow(), cancellationToken);
    }
}
