using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Setup;

[Authorize]
public sealed class RecoveryCodesModel : PageModel
{
    public IReadOnlyList<string> Codes { get; private set; } = [];

    public IActionResult OnGet()
    {
        var serializedCodes = TempData["RecoveryCodes"] as string;
        if (string.IsNullOrWhiteSpace(serializedCodes))
        {
            return RedirectToPage("/Index");
        }

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Codes = serializedCodes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Page();
    }
}
