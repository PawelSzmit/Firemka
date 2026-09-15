using System.ComponentModel.DataAnnotations;
using Firemka.Application.Sales;
using Firemka.Application.Time;
using Firemka.Domain.Sales;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Invoices.Sales;

[Authorize]
public sealed class DetailsModel(
    ISalesInvoiceService salesInvoiceService,
    TimeProvider timeProvider) : PageModel
{
    public SalesInvoiceDetails Details { get; private set; } = null!;

    [BindProperty]
    public EditInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        Input = new EditInput
        {
            NetAmount = Details.Invoice.NetAmount,
            VatRate = Details.VatRate,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (!await LoadAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        try
        {
            await salesInvoiceService.EditDraftAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                Input.NetAmount,
                Input.VatRate,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Zmieniono tylko tę wersję roboczą. Stawka na przyszłość pozostała bez zmian.";
            return RedirectToPage(new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(id, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRateDecisionAsync(
        Guid id,
        bool replaceManualAmount,
        CancellationToken cancellationToken)
    {
        try
        {
            await salesInvoiceService.ApplyPendingSubscriptionRateAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                replaceManualAmount,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = replaceManualAmount
                ? "Zastosowano nową stawkę abonamentu do tej wersji roboczej."
                : "Pozostawiono ręcznie wpisaną kwotę tej wersji roboczej.";
            return RedirectToPage(new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }
    }

    public async Task<IActionResult> OnPostIssueAsync(Guid id, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        try
        {
            var result = await salesInvoiceService.IssueAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                automatic: false,
                PolishBusinessTime.GetDate(nowUtc),
                nowUtc,
                cancellationToken);
            StatusMessage = result.Message;
            return RedirectToPage(new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }
    }

    public async Task<IActionResult> OnPostReopenRejectedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await salesInvoiceService.ReopenRejectedAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Odrzucona próba została zachowana w historii. Możesz teraz poprawić fakturę i wysłać ją jako nową próbę.";
            return RedirectToPage(new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var details = await salesInvoiceService.GetAsync(
            RequestIdentity.GetRequiredUserId(User),
            id,
            cancellationToken);
        if (details is null)
        {
            return false;
        }

        Details = details;
        return true;
    }

    public sealed class EditInput
    {
        [Range(
            typeof(decimal),
            "0.01",
            "9999999999999999",
            ErrorMessage = "Kwota netto musi być większa od zera.",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "Kwota netto")]
        public decimal NetAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "100",
            ErrorMessage = "Stawka VAT musi mieścić się od 0 do 100%.",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "Stawka VAT")]
        public decimal VatRate { get; set; }
    }
}
