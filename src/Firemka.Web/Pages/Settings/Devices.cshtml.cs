using Firemka.Application.Security;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settings;

[Authorize]
public sealed class DevicesModel(
    ITrustedDeviceService trustedDeviceService,
    IAuthenticationAuditService authenticationAuditService) : PageModel
{
    public IReadOnlyList<TrustedDeviceSummary> Devices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Devices = await trustedDeviceService.GetActiveAsync(
            RequestIdentity.GetRequiredUserId(User),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostRevokeAllAsync(CancellationToken cancellationToken)
    {
        var userId = RequestIdentity.GetRequiredUserId(User);
        await trustedDeviceService.RevokeAllAsync(userId, cancellationToken);
        TrustedDeviceCookie.Delete(Response, Request);
        await authenticationAuditService.RecordAsync(
            new AuthenticationAuditRecord(
                userId,
                AuthenticationAuditOutcome.TrustedDeviceRevoked,
                "trusted-device",
                RequestIdentity.GetIpAddress(HttpContext)),
            cancellationToken);

        return RedirectToPage();
    }
}
