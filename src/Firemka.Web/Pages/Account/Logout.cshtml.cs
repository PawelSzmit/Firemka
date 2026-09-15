using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Firemka.Infrastructure.Identity;
using Firemka.Web.Security;

namespace Firemka.Web.Pages.Account;

[Authorize]
public sealed class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        TrustedDeviceCookie.Delete(Response, Request);
        return RedirectToPage("/Account/Login");
    }
}
