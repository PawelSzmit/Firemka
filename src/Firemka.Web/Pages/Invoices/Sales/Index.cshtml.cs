using Firemka.Application.Sales;
using Firemka.Application.Time;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Invoices.Sales;

[Authorize]
public sealed class IndexModel(
    ISalesInvoiceService salesInvoiceService,
    KsefOutgoingOptions ksefOptions,
    TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<SalesInvoiceSummary> Invoices { get; private set; } = [];
    public SalesAutomationSnapshot Automation { get; private set; } = new(false, false, null);
    public bool IsKsefEnabled => ksefOptions.Enabled && ksefOptions.AdapterConfigured;

    [BindProperty]
    public bool AutomationWarningAcknowledged { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPrepareAsync(CancellationToken cancellationToken)
    {
        var ownerUserId = RequestIdentity.GetRequiredUserId(User);
        var nowUtc = timeProvider.GetUtcNow();
        var today = PolishBusinessTime.GetDate(nowUtc);
        try
        {
            var invoice = await salesInvoiceService.EnsureDraftAsync(
                ownerUserId,
                new DateOnly(today.Year, today.Month, 1),
                nowUtc,
                cancellationToken);
            StatusMessage = "Wersja robocza jest gotowa. Sprawdź ją przed wysłaniem.";
            return RedirectToPage("Details", new { id = invoice.Id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAutomationAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        try
        {
            await salesInvoiceService.SetAutomationAsync(
                RequestIdentity.GetRequiredUserId(User),
                enabled,
                AutomationWarningAcknowledged,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = enabled
                ? "Automatyzacja została włączona przez właściciela."
                : "Automatyzacja została wyłączona.";
            return RedirectToPage();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var ownerUserId = RequestIdentity.GetRequiredUserId(User);
        Invoices = await salesInvoiceService.ListAsync(ownerUserId, cancellationToken);
        try
        {
            Automation = await salesInvoiceService.GetAutomationAsync(ownerUserId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            Automation = new SalesAutomationSnapshot(false, false, null);
        }
    }
}
