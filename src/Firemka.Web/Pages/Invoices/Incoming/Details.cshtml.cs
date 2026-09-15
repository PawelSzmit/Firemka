using System.ComponentModel.DataAnnotations;
using Firemka.Application.Documents;
using Firemka.Domain.Documents;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Invoices.Incoming;

[Authorize]
public sealed class DetailsModel(IIncomingDocumentService incomingDocumentService) : PageModel
{
    public IncomingDocumentDetails Document { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public string? UnrelatedReason { get; set; }

    [BindProperty]
    public string? SourceConflictResolution { get; set; }

    public decimal? ConfidenceFor(string field) =>
        Document.FieldConfidences.TryGetValue(field, out var confidence)
            ? confidence
            : Document.Summary.Origin == SourceDocumentOrigin.Ksef
                ? Document.ExtractionConfidence
                : null;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        Input = new InputModel
        {
            InvoiceNumber = Document.Summary.InvoiceNumber,
            SellerName = Document.Summary.SellerName,
            SellerTaxId = Document.SellerTaxId,
            SellerAddress = Document.SellerAddress,
            IssueDate = Document.Summary.IssueDate,
            GrossAmount = Document.Summary.GrossAmount,
            Currency = Document.Summary.Currency ?? "PLN",
        };
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await incomingDocumentService.ConfirmAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                new DocumentData(
                    Input.InvoiceNumber,
                    Input.SellerName,
                    Input.SellerTaxId,
                    Input.IssueDate,
                    Input.GrossAmount,
                    Input.Currency,
                    Input.SellerAddress),
                DateTimeOffset.UtcNow,
                cancellationToken);
            TempData["StatusMessage"] = "Dane dokumentu zostały potwierdzone.";
            return RedirectToPage(new { id });
        }
        catch (DocumentDataValidationException exception)
        {
            foreach (var error in exception.Errors)
            {
                ModelState.AddModelError(string.Empty, $"Uzupełnij: {error}.");
            }

            return Page();
        }
    }

    public async Task<IActionResult> OnPostUnrelatedAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith("Input.", StringComparison.Ordinal)).ToArray())
        {
            ModelState.Remove(key);
        }

        if (string.IsNullOrWhiteSpace(UnrelatedReason))
        {
            ModelState.AddModelError(
                nameof(UnrelatedReason),
                "Podaj powód oznaczenia dokumentu jako niezwiązanego z firmą.");
        }
        else if (UnrelatedReason.Length > 1_000)
        {
            ModelState.AddModelError(nameof(UnrelatedReason), "Powód może mieć maksymalnie 1000 znaków.");
        }

        if (!ModelState.IsValid)
        {
            if (!await LoadAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }

        await incomingDocumentService.MarkUnrelatedAsync(
            RequestIdentity.GetRequiredUserId(User),
            id,
            UnrelatedReason!,
            DateTimeOffset.UtcNow,
            cancellationToken);
        TempData["StatusMessage"] = "Dokument oznaczono jako niezwiązany z firmą, a decyzję zapisano w audycie.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResolveConflictAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(SourceConflictResolution))
        {
            ModelState.AddModelError(
                nameof(SourceConflictResolution),
                "Wyjaśnij, dlaczego zachowane źródło jest prawidłowe i konflikt można zamknąć.");
        }
        else if (SourceConflictResolution.Length > 2_000)
        {
            ModelState.AddModelError(
                nameof(SourceConflictResolution),
                "Wyjaśnienie może mieć maksymalnie 2000 znaków.");
        }

        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await incomingDocumentService.ResolveSourceConflictAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                SourceConflictResolution!,
                DateTimeOffset.UtcNow,
                cancellationToken);
            TempData["StatusMessage"] =
                "Konflikt źródła został wyjaśniony. Historia wykrycia i decyzji została zachowana.";
            return RedirectToPage(new { id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(SourceConflictResolution), exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await incomingDocumentService.GetAsync(
            RequestIdentity.GetRequiredUserId(User),
            id,
            cancellationToken);
        if (document is null)
        {
            return false;
        }

        Document = document;
        return true;
    }

    public sealed class InputModel
    {
        [Display(Name = "Numer faktury")]
        [Required(ErrorMessage = "Podaj numer faktury.")]
        [StringLength(256)]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "Sprzedawca")]
        [Required(ErrorMessage = "Podaj sprzedawcę.")]
        [StringLength(300)]
        public string? SellerName { get; set; }

        [Display(Name = "NIP sprzedawcy")]
        [StringLength(32)]
        public string? SellerTaxId { get; set; }

        [Display(Name = "Adres sprzedawcy")]
        [StringLength(500)]
        public string? SellerAddress { get; set; }

        [Display(Name = "Data wystawienia")]
        [Required(ErrorMessage = "Podaj datę wystawienia.")]
        public DateOnly? IssueDate { get; set; }

        [Display(Name = "Kwota brutto")]
        [Required(ErrorMessage = "Podaj kwotę brutto.")]
        public decimal? GrossAmount { get; set; }

        [Display(Name = "Waluta")]
        [Required(ErrorMessage = "Podaj walutę.")]
        [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Waluta musi mieć trzy litery, np. PLN.")]
        public string? Currency { get; set; }
    }
}
