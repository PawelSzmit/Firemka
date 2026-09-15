using System.ComponentModel.DataAnnotations;
using Firemka.Application.Security;
using Firemka.Infrastructure.Identity;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Firemka.Web.Pages.Setup;

[AllowAnonymous]
[EnableRateLimiting("login")]
public sealed class ResumeModel(
    OwnerBootstrapService ownerBootstrapService,
    OwnerSetupTicketService ownerSetupTicketService,
    IAuthenticationAuditService authenticationAuditService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await ownerBootstrapService.GetStatusAsync(cancellationToken) == OwnerBootstrapStatus.AwaitingTwoFactorSetup
            ? Page()
            : RedirectToPage("/Setup/Index");
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await ownerBootstrapService.ResumeAsync(Input.Email, Input.Password, cancellationToken);
        if (user is null)
        {
            await RecordAuditAsync(null, AuthenticationAuditOutcome.Failed, cancellationToken);
            ModelState.AddModelError(string.Empty, "Nieprawidłowy e-mail lub hasło.");
            return Page();
        }

        await RecordAuditAsync(user.Id, AuthenticationAuditOutcome.Successful, cancellationToken);
        ownerSetupTicketService.Issue(Response, Request, user.Id);
        return RedirectToPage("/Setup/TwoFactor");
    }

    private Task RecordAuditAsync(
        string? userId,
        AuthenticationAuditOutcome outcome,
        CancellationToken cancellationToken) =>
        authenticationAuditService.RecordAsync(
            new AuthenticationAuditRecord(
                userId,
                outcome,
                "setup-resume-password",
                RequestIdentity.GetIpAddress(HttpContext)),
            cancellationToken);

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
