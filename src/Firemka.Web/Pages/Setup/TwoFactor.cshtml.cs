using System.ComponentModel.DataAnnotations;
using Firemka.Application.Security;
using Firemka.Infrastructure.Identity;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;

namespace Firemka.Web.Pages.Setup;

[AllowAnonymous]
[EnableRateLimiting("login")]
public sealed class TwoFactorModel(
    OwnerBootstrapService ownerBootstrapService,
    OwnerSetupTicketService ownerSetupTicketService,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuthenticationAuditService authenticationAuditService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string AuthenticatorUri { get; private set; } = string.Empty;

    public string AuthenticatorQrCodeDataUri { get; private set; } = string.Empty;

    public string SharedKey { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await GetSetupUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Setup/Index");
        }

        PreventCaching();
        await LoadAuthenticatorDetailsAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await GetSetupUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Setup/Index");
        }

        PreventCaching();
        if (!ModelState.IsValid)
        {
            await LoadAuthenticatorDetailsAsync(user);
            return Page();
        }

        var code = Input.Code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            code);

        if (!isValid)
        {
            ModelState.AddModelError(string.Empty, "Kod jest nieprawidłowy. Spróbuj ponownie.");
            await LoadAuthenticatorDetailsAsync(user);
            return Page();
        }

        var enabled = await userManager.SetTwoFactorEnabledAsync(user, enabled: true);
        if (!enabled.Succeeded)
        {
            foreach (var error in enabled.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await LoadAuthenticatorDetailsAsync(user);
            return Page();
        }

        var generatedRecoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        if (generatedRecoveryCodes is null)
        {
            throw new InvalidOperationException("Nie udało się wygenerować kodów odzyskiwania.");
        }

        var recoveryCodes = generatedRecoveryCodes.ToArray();
        await ownerBootstrapService.MarkSetupCompleteAsync(user.Id, cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: false);
        await authenticationAuditService.RecordAsync(
            new AuthenticationAuditRecord(user.Id, AuthenticationAuditOutcome.Successful, "totp-initial-setup", RequestIdentity.GetIpAddress(HttpContext)),
            cancellationToken);

        TempData["RecoveryCodes"] = string.Join('|', recoveryCodes);
        ownerSetupTicketService.Clear(Response, Request);
        return RedirectToPage("/Setup/RecoveryCodes");
    }

    private async Task<ApplicationUser?> GetSetupUserAsync(CancellationToken cancellationToken)
    {
        var userId = ownerSetupTicketService.TryRead(Request);
        return userId is null
            ? null
            : await ownerBootstrapService.GetUnfinishedOwnerAsync(userId, cancellationToken);
    }

    private async Task LoadAuthenticatorDetailsAsync(ApplicationUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            var reset = await userManager.ResetAuthenticatorKeyAsync(user);
            if (!reset.Succeeded)
            {
                throw new InvalidOperationException("Nie udało się przygotować klucza TOTP.");
            }

            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(key!);
        var issuer = Uri.EscapeDataString("Firemka");
        var accountName = Uri.EscapeDataString($"Firemka:{user.Email}");
        AuthenticatorUri = $"otpauth://totp/{accountName}?secret={key}&issuer={issuer}&digits=6";
        AuthenticatorQrCodeDataUri = CreateQrCodeDataUri(AuthenticatorUri);
    }

    private static string CreateQrCodeDataUri(string authenticatorUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(authenticatorUri, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        return $"data:image/png;base64,{Convert.ToBase64String(code.GetGraphic(8))}";
    }

    private static string FormatKey(string key)
    {
        return string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4)
            .Select(index => key.Substring(index * 4, Math.Min(4, key.Length - (index * 4)))));
    }

    private void PreventCaching()
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
    }

    public sealed class InputModel
    {
        [Display(Name = "Kod z aplikacji")]
        [Required(ErrorMessage = "Podaj kod z aplikacji.")]
        public string Code { get; set; } = string.Empty;
    }
}
