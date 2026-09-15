using System.ComponentModel.DataAnnotations;
using Firemka.Application.MonthClosing;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Domain.Calculations;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settlements;

[Authorize]
public sealed class MonthModel(
    ICompanyProfileService companyProfileService,
    IMonthClosingService monthClosingService,
    TimeProvider timeProvider) : PageModel
{
    public MonthClosingView Closing { get; private set; } = null!;

    public int SelectedYear { get; private set; }

    public int SelectedMonth { get; private set; }

    [BindProperty]
    public DeclarationInput Declaration { get; set; } = new();

    [BindProperty]
    public AdjustmentInput Adjustment { get; set; } = new();

    [BindProperty]
    public CorrectionInput Correction { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        SetDeclarationDefaults();
        return Page();
    }

    public async Task<IActionResult> OnPostDeclarationAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Declaration));
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        ValidateDeclaration();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await monthClosingService.SaveDeclarationAsync(
                UserId(),
                Closing.CompanyId,
                Closing.Month,
                new SaveMonthDeclarationCommand(
                    Declaration.SocialContributionsDeductible,
                    Declaration.PitBaseAdjustment,
                    Declaration.HealthIncomeAdjustment,
                    Closing.IsFirstBusinessMonth ? Declaration.OpeningVatCarryForward : null,
                    Closing.IsFirstBusinessMonth ? Declaration.OpeningPitAdvancesDue : null,
                    Closing.IsFirstBusinessMonth && Declaration.OpeningBalancesConfirmed,
                    Declaration.HealthIncomeConfirmed,
                    Declaration.EvidenceReference),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Dane miesięczne zostały zapisane jako nowa, niezmienna wersja.";
            return RedirectToSelectedMonth();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAdjustmentAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Adjustment));
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        if (Adjustment.Kind != MonthAdjustmentKind.SalesRecognition && Adjustment.Amount == 0m)
        {
            ModelState.AddModelError(
                $"{nameof(Adjustment)}.{nameof(AdjustmentInput.Amount)}",
                "Kwota korekty nie może wynosić zero.");
        }


        if (Adjustment.Kind == MonthAdjustmentKind.SalesRecognition && Adjustment.Amount != 0m)
        {
            ModelState.AddModelError(
                $"{nameof(Adjustment)}.{nameof(AdjustmentInput.Amount)}",
                "Potwierdzenie okresu sprzedaży nie może zmieniać kwoty rozliczenia.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await monthClosingService.AddAdjustmentAsync(
                UserId(),
                Closing.CompanyId,
                Closing.Month,
                new AddMonthAdjustmentCommand(
                    Adjustment.Kind,
                    Adjustment.Amount,
                    Adjustment.Reason,
                    Adjustment.EvidenceReference,
                    Adjustment.SourceDocumentId,
                    Adjustment.SalesInvoiceId),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Korekta została dopisana bez zmiany wcześniejszych danych.";
            return RedirectToSelectedMonth("adjustments");
        }
        catch (KeyNotFoundException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCloseAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        ModelState.Clear();
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        try
        {
            var settlement = await monthClosingService.CloseAsync(
                UserId(),
                Closing.CompanyId,
                Closing.Month,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = $"Miesiąc zamknięto jako wersję {settlement.VersionNumber}.";
            return RedirectToSelectedMonth("history");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (MonthClosingBlockedException exception)
        {
            foreach (var blocker in exception.Blockers)
            {
                ModelState.AddModelError(string.Empty, blocker.Message);
            }

            await LoadAsync(year, month, cancellationToken);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(year, month, cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCorrectionAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Correction));
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var settlement = await monthClosingService.StartCorrectionAsync(
                UserId(),
                Closing.CompanyId,
                Closing.Month,
                Correction.Reason,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = $"Otwarto korektę w wersji {settlement.VersionNumber}. Wcześniejsze zamknięcie pozostało bez zmian.";
            return RedirectToSelectedMonth("adjustments");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        var today = PolishBusinessTime.Today(timeProvider);
        SelectedYear = year is >= 2000 and <= 2200 ? year.Value : today.Year;
        SelectedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        var profile = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (profile is null)
        {
            return false;
        }

        var selected = new DateOnly(SelectedYear, SelectedMonth, 1);
        var closing = await monthClosingService.GetAsync(
            UserId(),
            profile.Id,
            selected,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (closing is null)
        {
            return false;
        }

        Closing = closing;
        return true;
    }

    private void SetDeclarationDefaults()
    {
        if (Closing.Declaration is { } current)
        {
            Declaration = new DeclarationInput
            {
                SocialContributionsDeductible = current.SocialContributionsDeductible,
                PitBaseAdjustment = current.PitBaseAdjustment,
                HealthIncomeAdjustment = current.HealthIncomeAdjustment,
                OpeningVatCarryForward = current.OpeningVatCarryForward,
                OpeningPitAdvancesDue = current.OpeningPitAdvancesDue,
                OpeningBalancesConfirmed = current.OpeningBalancesConfirmed,
                HealthIncomeConfirmed = current.HealthIncomeConfirmed,
                EvidenceReference = current.EvidenceReference,
            };
            return;
        }

        Declaration = new DeclarationInput
        {
            OpeningVatCarryForward = Closing.IsFirstBusinessMonth ? 0m : null,
            OpeningPitAdvancesDue = Closing.IsFirstBusinessMonth ? 0m : null,
        };
    }

    private void ValidateDeclaration()
    {
        if (Declaration.SocialContributionsDeductible < 0m)
        {
            ModelState.AddModelError(
                $"{nameof(Declaration)}.{nameof(DeclarationInput.SocialContributionsDeductible)}",
                "Składki społeczne nie mogą być ujemne.");
        }

        if (!Declaration.HealthIncomeConfirmed)
        {
            ModelState.AddModelError(
                $"{nameof(Declaration)}.{nameof(DeclarationInput.HealthIncomeConfirmed)}",
                "Potwierdź osobne dane dochodu do składki zdrowotnej.");
        }

        if (!Closing.IsFirstBusinessMonth)
        {
            return;
        }

        if (Declaration.OpeningVatCarryForward is null || Declaration.OpeningVatCarryForward < 0m)
        {
            ModelState.AddModelError(
                $"{nameof(Declaration)}.{nameof(DeclarationInput.OpeningVatCarryForward)}",
                "Podaj nieujemne saldo VAT na początek, także gdy wynosi zero.");
        }

        if (Declaration.OpeningPitAdvancesDue is null || Declaration.OpeningPitAdvancesDue < 0m)
        {
            ModelState.AddModelError(
                $"{nameof(Declaration)}.{nameof(DeclarationInput.OpeningPitAdvancesDue)}",
                "Podaj nieujemną sumę wcześniejszych zaliczek PIT, także gdy wynosi zero.");
        }

        if (!Declaration.OpeningBalancesConfirmed)
        {
            ModelState.AddModelError(
                $"{nameof(Declaration)}.{nameof(DeclarationInput.OpeningBalancesConfirmed)}",
                "Potwierdź salda otwarcia, także gdy oba wynoszą zero.");
        }
    }

    private IActionResult RedirectToSelectedMonth(string? fragment = null)
        => Redirect($"/Settlements/Month?year={SelectedYear}&month={SelectedMonth}"
            + (string.IsNullOrWhiteSpace(fragment) ? string.Empty : $"#{fragment}"));

    private string UserId() => RequestIdentity.GetRequiredUserId(User);

    private void KeepOnlyModelState(string prefix)
    {
        foreach (var key in ModelState.Keys
            .Where(key => !key.StartsWith(prefix + ".", StringComparison.Ordinal))
            .ToArray())
        {
            ModelState.Remove(key);
        }
    }

    public sealed class DeclarationInput
    {
        [Display(Name = "Składki społeczne zapłacone poza KPiR")]
        public decimal SocialContributionsDeductible { get; set; }

        [Display(Name = "Korekta podstawy PIT")]
        public decimal PitBaseAdjustment { get; set; }

        [Display(Name = "Korekta dochodu do składki zdrowotnej")]
        public decimal HealthIncomeAdjustment { get; set; }

        [Display(Name = "VAT do przeniesienia na początek")]
        public decimal? OpeningVatCarryForward { get; set; }

        [Display(Name = "Wcześniejsze zaliczki PIT na początek")]
        public decimal? OpeningPitAdvancesDue { get; set; }

        [Display(Name = "Potwierdzam salda otwarcia, także jeśli wynoszą zero")]
        public bool OpeningBalancesConfirmed { get; set; }

        [Display(Name = "Potwierdzam osobne dane dochodu do składki zdrowotnej")]
        public bool HealthIncomeConfirmed { get; set; }

        [Required(ErrorMessage = "Podaj źródło lub opis potwierdzenia.")]
        [StringLength(2_000)]
        [Display(Name = "Źródło lub opis sprawdzenia")]
        public string EvidenceReference { get; set; } = string.Empty;
    }

    public sealed class AdjustmentInput
    {
        [Display(Name = "Rodzaj korekty")]
        public MonthAdjustmentKind Kind { get; set; } = MonthAdjustmentKind.HealthIncome;

        [Display(Name = "Kwota ze znakiem")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Podaj powód korekty.")]
        [StringLength(2_000)]
        [Display(Name = "Powód")]
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Podaj źródło lub opis dowodu korekty.")]
        [StringLength(2_000)]
        [Display(Name = "Źródło lub opis dowodu")]
        public string EvidenceReference { get; set; } = string.Empty;

        [Display(Name = "Identyfikator dokumentu kosztowego — opcjonalnie")]
        public Guid? SourceDocumentId { get; set; }

        [Display(Name = "Identyfikator faktury sprzedaży — opcjonalnie")]
        public Guid? SalesInvoiceId { get; set; }
    }

    public sealed class CorrectionInput
    {
        [Required(ErrorMessage = "Podaj powód rozpoczęcia korekty.")]
        [StringLength(2_000)]
        [Display(Name = "Powód korekty")]
        public string Reason { get; set; } = string.Empty;
    }
}
