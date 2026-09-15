using Firemka.Application.Month;
using Firemka.Application.Time;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Month;

[Authorize]
public sealed class IndexModel(IMonthDashboardQuery dashboardQuery, TimeProvider timeProvider) : PageModel
{
    public MonthDashboard Dashboard { get; private set; } = null!;

    public async Task OnGetAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        var today = PolishBusinessTime.Today(timeProvider);
        var selectedYear = year is >= 2000 and <= 2200 ? year.Value : today.Year;
        var selectedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        Dashboard = await dashboardQuery.GetAsync(
            RequestIdentity.GetRequiredUserId(User),
            selectedYear,
            selectedMonth,
            cancellationToken);
    }
}
