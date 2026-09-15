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
public sealed class LoginModel(
    OwnerBootstrapService ownerBootstrapService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITrustedDeviceService trustedDeviceService,
    IAuthenticationAuditService authenticationAuditService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var setupStatus = await ownerBootstrapService.GetStatusAsync(cancellationToken);
        return setupStatus switch
        {
            OwnerBootstrapStatus.ReadyToStart => RedirectToPage("/Setup/Index"),
            OwnerBootstrapStatus.AwaitingTwoFactorSetup => RedirectToPage("/Setup/Resume"),
            _ when User.Identity?.IsAuthenticated == true => RedirectToLocalDestination(),
            _ => Page(),
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await ownerBootstrapService.GetStatusAsync(cancellationToken) != OwnerBootstrapStatus.Complete)
        {
            return RedirectToPage("/Setup/Index");
        }

        var user = await userManager.FindByEmailAsync(Input.Email.Trim());
        if (user is null)
        {
            await RecordAuditAsync(null, AuthenticationAuditOutcome.Failed, "password", cancellationToken);
            AddGenericLoginError();
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            Input.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            var trustedToken = TrustedDeviceCookie.TryRead(Request);
            if (trustedToken is not null
                && await trustedDeviceService.ValidateAndTouchAsync(user.Id, trustedToken, cancellationToken))
            {
                await signInManager.SignInAsync(user, isPersistent: false);
                await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.Successful, "trusted-device", cancellationToken);
                return RedirectToLocalDestination();
            }

            return RedirectToPage("/Account/TwoFactor", new { returnUrl = ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.LockedOut, "password", cancellationToken);
            ModelState.AddModelError(string.Empty, "Zbyt wiele nieudanych prób. Spróbuj ponownie za 15 minut.");
            return Page();
        }

        if (result.Succeeded)
        {
            await signInManager.SignOutAsync();
        }

        await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.Failed, "password", cancellationToken);
        AddGenericLoginError();
        return Page();
    }

    private void AddGenericLoginError()
    {
        ModelState.AddModelError(string.Empty, "Nieprawidłowy e-mail, hasło lub konfiguracja zabezpieczeń.");
    }

    private async Task RecordAuditAsync(
        string? userId,
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
        [Display(Name = "E-mail")]
        [EmailAddress]
        [Required(ErrorMessage = "Podaj e-mail.")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        [Required(ErrorMessage = "Podaj hasło.")]
        public string Password { get; set; } = string.Empty;
    }
}
