using Firemka.Application.Onboarding;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages;

public class IndexModel(ICompanyProfileService companyProfileService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var configured = await companyProfileService.ExistsAsync(
            RequestIdentity.GetRequiredUserId(User),
            cancellationToken);
        return configured
            ? RedirectToPage("/Month/Index")
            : RedirectToPage("/Setup/Company");
    }
}
