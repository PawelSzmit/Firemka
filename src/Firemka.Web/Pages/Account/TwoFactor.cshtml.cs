using System.ComponentModel.DataAnnotations;
using Firemka.Application.Security;
using Firemka.Infrastructure.Identity;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Firemka.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("login")]
public sealed class TwoFactorModel(
    SignInManager<ApplicationUser> signInManager,
    ITrustedDeviceService trustedDeviceService,
    IAuthenticationAuditService authenticationAuditService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await signInManager.GetTwoFactorAuthenticationUserAsync() is null
            ? RedirectToPage("/Account/Login")
            : Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var code = Input.UseRecoveryCode
            ? Input.Code.Trim()
            : Input.Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var result = Input.UseRecoveryCode
            ? await signInManager.TwoFactorRecoveryCodeSignInAsync(code)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);

        if (!result.Succeeded)
        {
            await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.Failed, Input.UseRecoveryCode ? "recovery-code" : "totp", cancellationToken);
            ModelState.AddModelError(string.Empty, "Kod jest nieprawidłowy albo został już wykorzystany.");
            return Page();
        }

        if (Input.TrustThisDevice)
        {
            var issue = await trustedDeviceService.IssueAsync(
                user.Id,
                RequestIdentity.GetDeviceName(Request),
                cancellationToken);
            TrustedDeviceCookie.Write(Response, Request, issue);
        }

        await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.Successful, Input.UseRecoveryCode ? "recovery-code" : "totp", cancellationToken);
        return RedirectToLocalDestination();
    }

    private async Task RecordAuditAsync(
        string userId,
        AuthenticationAuditOutcome outcome,
        string method,
        CancellationToken cancellationToken)
    {
        await authenticationAuditService.RecordAsync(
            new AuthenticationAuditRecord(userId, outcome, method, RequestIdentity.GetIpAddress(HttpContext)),
            cancellationToken);
    }

    private IActionResult RedirectToLocalDestination()
    {
        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage("/Index");
    }

    public sealed class InputModel
    {
        [Display(Name = "Kod")]
        [Required(ErrorMessage = "Podaj kod.")]
        public string Code { get; set; } = string.Empty;

        public bool TrustThisDevice { get; set; }

        public bool UseRecoveryCode { get; set; }
    }
}
