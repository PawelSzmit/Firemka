using System.ComponentModel.DataAnnotations;
using Firemka.Infrastructure.Identity;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Firemka.Web.Pages.Setup;

[AllowAnonymous]
[EnableRateLimiting("login")]
public sealed class IndexModel(
    OwnerBootstrapService ownerBootstrapService,
    OwnerSetupTicketService ownerSetupTicketService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await ownerBootstrapService.GetStatusAsync(cancellationToken) switch
        {
            OwnerBootstrapStatus.AwaitingTwoFactorSetup => RedirectToPage("/Setup/Resume"),
            OwnerBootstrapStatus.Complete => RedirectToPage("/Account/Login"),
            _ => Page(),
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!string.Equals(Input.Password, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.ConfirmPassword), "Hasła muszą być takie same.");
            return Page();
        }

        var result = await ownerBootstrapService.TryCreateOwnerAsync(
            Input.Email,
            Input.Password,
            cancellationToken);

        if (result.Status == OwnerCreationStatus.Created && result.UserId is not null)
        {
            ownerSetupTicketService.Issue(Response, Request, result.UserId);
            return RedirectToPage("/Setup/TwoFactor");
        }

        if (result.Status == OwnerCreationStatus.AlreadyStarted)
        {
            return RedirectToPage("/Setup/Resume");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Display(Name = "E-mail właściciela")]
        [EmailAddress]
        [Required(ErrorMessage = "Podaj e-mail.")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        [Required(ErrorMessage = "Podaj hasło.")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Powtórz hasło")]
        [Required(ErrorMessage = "Powtórz hasło.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
