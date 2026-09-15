using Firemka.Application.Documents;
using Firemka.Domain.Documents;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Expenses;

[Authorize]
public sealed class IndexModel(IIncomingDocumentService incomingDocumentService) : PageModel
{
    public IReadOnlyList<IncomingDocumentSummary> Documents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var page = await incomingDocumentService.ListAsync(
            RequestIdentity.GetRequiredUserId(User),
            new IncomingDocumentListQuery(1, 50, SourceDocumentStatus.RuleToDefine),
            cancellationToken);
        Documents = page.Items;
    }
}
