using System.ComponentModel.DataAnnotations;
using Firemka.Application.Accounting;
using Firemka.Application.Documents;
using Firemka.Domain.Accounting;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Expenses;

[Authorize]
public sealed class ReviewModel(
    IIncomingDocumentService incomingDocumentService,
    ICostAccountingService costAccountingService,
    TimeProvider timeProvider) : PageModel
{
    public IncomingDocumentDetails Document { get; private set; } = null!;
    public CostReviewSnapshot? Review { get; private set; }

    public PreparationInput Preparation { get; set; } = new();

    public DecisionInput Decision { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (Review is null)
        {
            Preparation.GrossAmount = Document.Summary.GrossAmount;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPrepareAsync(
        Guid id,
        [Bind(Prefix = "Preparation")] PreparationInput preparation,
        CancellationToken cancellationToken)
    {
        Preparation = preparation;
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid
            || Preparation.VatTreatment is null
            || Preparation.ServiceKind is null
            || Preparation.GrossAmount is null
            || Preparation.InputVatAmount is null)
        {
            return Page();
        }

        try
        {
            await costAccountingService.PrepareAsync(
                RequestIdentity.GetRequiredUserId(User),
                id,
                new PrepareCostCommand(
                    Preparation.SellerCountryCode,
                    Preparation.VatTreatment.Value,
                    Preparation.VatRate,
                    Preparation.ServiceKind.Value,
                    Preparation.GrossAmount.Value,
                    Preparation.InputVatAmount.Value,
                    Preparation.SellerNameFallbackConfirmed),
                timeProvider.GetUtcNow(),
                cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public Task<IActionResult> OnPostConfirmDocumentAsync(
        Guid id,
        [Bind(Prefix = "Decision")] DecisionInput decision,
        CancellationToken cancellationToken)
        => ConfirmAsync(id, decision, applyFuture: false, cancellationToken);

    public Task<IActionResult> OnPostConfirmFutureAsync(
        Guid id,
        [Bind(Prefix = "Decision")] DecisionInput decision,
        CancellationToken cancellationToken)
        => ConfirmAsync(id, decision, applyFuture: true, cancellationToken);

    private async Task<IActionResult> ConfirmAsync(
        Guid id,
        DecisionInput decision,
        bool applyFuture,
        CancellationToken cancellationToken)
    {
        Decision = decision;
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (Review is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid
            || Decision.VatDeductionPercent is null
            || Decision.KpirCostPercent is null
            || Decision.KpirPeriodPolicy is null
            || Decision.VatPeriodPolicy is null
            || Decision.KpirPeriod is null
            || Decision.VatPeriod is null)
        {
            return Page();
        }

        var command = new CostDecisionCommand(
            Decision.KpirCategory,
            Decision.VatDeductionPercent.Value,
            Decision.KpirCostPercent.Value,
            Decision.KpirPeriodPolicy.Value,
            Decision.VatPeriodPolicy.Value,
            Decision.KpirPeriod.Value,
            Decision.VatPeriod.Value,
            Decision.DecisionSource);
        try
        {
            if (applyFuture)
            {
                await costAccountingService.ConfirmAndApplyFutureAsync(
                    RequestIdentity.GetRequiredUserId(User), Review.Booking.Id, command, timeProvider.GetUtcNow(), cancellationToken);
                StatusMessage = "Dokument zaksięgowano, a regułę zapisano dla pełnych przyszłych dopasowań.";
            }
            else
            {
                await costAccountingService.ConfirmForDocumentAsync(
                    RequestIdentity.GetRequiredUserId(User), Review.Booking.Id, command, timeProvider.GetUtcNow(), cancellationToken);
                StatusMessage = "Dokument zaksięgowano bez tworzenia reguły na przyszłość.";
            }

            return RedirectToPage("Index");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = RequestIdentity.GetRequiredUserId(User);
        var document = await incomingDocumentService.GetAsync(owner, id, cancellationToken);
        if (document is null)
        {
            return false;
        }

        Document = document;
        Review = await costAccountingService.GetReviewAsync(owner, id, cancellationToken);
        return true;
    }

    public sealed class PreparationInput
    {
        [Required(ErrorMessage = "Podaj kraj sprzedawcy.")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Kraj musi mieć dwa znaki.")]
        [Display(Name = "Kraj sprzedawcy")]
        public string SellerCountryCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wybierz profil VAT.")]
        [Display(Name = "Profil VAT")]
        public VatTreatment? VatTreatment { get; set; }

        [Range(0, 100, ErrorMessage = "Stawka VAT musi mieścić się od 0 do 100.")]
        [Display(Name = "Stawka VAT (%)")]
        public decimal? VatRate { get; set; }

        [Required(ErrorMessage = "Wybierz rodzaj kosztu.")]
        [Display(Name = "Rodzaj usługi lub kosztu")]
        public CostServiceKind? ServiceKind { get; set; }

        [Required]
        [Range(
            typeof(decimal),
            "0.01",
            "9999999999999999",
            ErrorMessage = "Kwota brutto musi być większa od zera.",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "Kwota brutto")]
        public decimal? GrossAmount { get; set; }

        [Required]
        [Range(
            typeof(decimal),
            "0",
            "9999999999999999",
            ErrorMessage = "VAT naliczony nie może być ujemny.",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "VAT naliczony")]
        public decimal? InputVatAmount { get; set; }

        [Display(Name = "Jeśli dokument nie ma NIP-u, zgadzam się awaryjnie dopasowywać sprzedawcę po nazwie")]
        public bool SellerNameFallbackConfirmed { get; set; }
    }

    public sealed class DecisionInput
    {
        [Required(ErrorMessage = "Podaj kategorię KPiR.")]
        [StringLength(120)]
        [Display(Name = "Kategoria KPiR")]
        public string KpirCategory { get; set; } = string.Empty;

        [Required(ErrorMessage = "Podaj procent odliczenia VAT.")]
        [Range(0, 100)]
        [Display(Name = "Odliczenie VAT (%)")]
        public decimal? VatDeductionPercent { get; set; }

        [Required(ErrorMessage = "Podaj procent kosztu KPiR.")]
        [Range(0, 100)]
        [Display(Name = "Koszt KPiR (%)")]
        public decimal? KpirCostPercent { get; set; }

        [Required(ErrorMessage = "Wybierz politykę okresu KPiR.")]
        [Display(Name = "Okres KPiR")]
        public AccountingPeriodPolicy? KpirPeriodPolicy { get; set; }

        [Required(ErrorMessage = "Wybierz politykę okresu VAT.")]
        [Display(Name = "Okres VAT")]
        public AccountingPeriodPolicy? VatPeriodPolicy { get; set; }

        [Required]
        [Display(Name = "Wybrany miesiąc KPiR")]
        public DateOnly? KpirPeriod { get; set; }

        [Required]
        [Display(Name = "Wybrany miesiąc VAT")]
        public DateOnly? VatPeriod { get; set; }

        [Required(ErrorMessage = "Podaj źródło albo uzasadnienie decyzji.")]
        [StringLength(1000)]
        [Display(Name = "Źródło lub uzasadnienie")]
        public string DecisionSource { get; set; } = string.Empty;
    }
}
