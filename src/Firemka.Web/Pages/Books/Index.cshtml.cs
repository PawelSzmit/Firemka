using Firemka.Application.Accounting;
using Firemka.Application.Time;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Books;

[Authorize]
public sealed class IndexModel(ICostAccountingService costAccountingService, TimeProvider timeProvider) : PageModel
{
    public int Year { get; private set; }
    public IReadOnlyList<KpirEntrySnapshot> Kpir { get; private set; } = [];
    public IReadOnlyList<VatEntrySnapshot> Vat { get; private set; } = [];

    public async Task OnGetAsync(int? year, CancellationToken cancellationToken)
    {
        var current = PolishBusinessTime.Today(timeProvider).Year;
        Year = year is >= 2000 and <= 2200 ? year.Value : current;
        var owner = RequestIdentity.GetRequiredUserId(User);
        Kpir = await costAccountingService.ListKpirAsync(owner, Year, cancellationToken);
        Vat = await costAccountingService.ListVatAsync(owner, Year, cancellationToken);
    }
}
