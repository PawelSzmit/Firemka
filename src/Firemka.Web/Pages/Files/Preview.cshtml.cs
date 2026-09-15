using Firemka.Infrastructure.Files;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Files;

[Authorize]
public sealed class PreviewModel(StoredFileService storedFileService) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, bool download, CancellationToken cancellationToken)
    {
        var storedFile = await storedFileService.OpenAsync(
            id,
            RequestIdentity.GetRequiredUserId(User),
            cancellationToken);
        if (storedFile is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        return new FileStreamResult(storedFile.Content, storedFile.MediaType)
        {
            EnableRangeProcessing = true,
            FileDownloadName = download ? storedFile.OriginalFileName : null,
        };
    }
}
