using System.ComponentModel.DataAnnotations;
using Firemka.Application.AnnualClosing;
using Firemka.Application.Filings;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Filings;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settlements;

[Authorize]
public sealed class YearModel(
    ICompanyProfileService companyProfileService,
    IAnnualClosingService annualClosingService,
    TimeProvider timeProvider) : PageModel
{
    public AnnualClosingView Workspace { get; private set; } = null!;
    public int SelectedYear { get; private set; }

    [BindProperty]
    public DeclarationInput Declaration { get; set; } = new();

    [BindProperty]
    public CorrectionInput Correction { get; set; } = new();

    [BindProperty]
    public CloseInput CloseConfirmation { get; set; } = new();

    [BindProperty]
    public JpkActionInput JpkAction { get; set; } = new();

    [BindProperty]
    public JpkOutcomeInput JpkOutcome { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (Workspace.Declaration is { } declaration)
        {
            Declaration.OpeningInventory = declaration.OpeningInventory;
            Declaration.ClosingInventory = declaration.ClosingInventory;
            Declaration.PitAdvancesPaid = declaration.PitAdvancesPaid;
            Declaration.HealthContributionsPaid = declaration.HealthContributionsPaid;
            Declaration.EvidenceReference = declaration.EvidenceReference;
            Declaration.ConfirmedOn = declaration.ConfirmedOn;
            Declaration.IndependentVerificationConfirmed = true;
        }
        else
        {
            Declaration.ConfirmedOn = PolishBusinessTime.Today(timeProvider);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostDeclarationAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(Declaration));
        if (!Declaration.IndependentVerificationConfirmed)
            ModelState.AddModelError($"{nameof(Declaration)}.{nameof(Declaration.IndependentVerificationConfirmed)}", "Zaznacz niezależne sprawdzenie.");
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await annualClosingService.SaveDeclarationAsync(UserId(), Workspace.CompanyId, SelectedYear,
                new SaveAnnualDeclarationCommand(
                    Declaration.OpeningInventory, Declaration.ClosingInventory,
                    Declaration.PitAdvancesPaid, Declaration.HealthContributionsPaid,
                    Declaration.IndependentVerificationConfirmed, Declaration.EvidenceReference,
                    Declaration.ConfirmedOn!.Value), Now(), cancellationToken);
            StatusMessage = "Potwierdzone dane roczne zostały zapisane jako nowa wersja.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCloseAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(CloseConfirmation));
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!CloseConfirmation.IndependentResultConfirmed)
            ModelState.AddModelError($"{nameof(CloseConfirmation)}.{nameof(CloseConfirmation.IndependentResultConfirmed)}",
                "Potwierdź niezależne sprawdzenie rocznego podsumowania.");
        if (!ModelState.IsValid) return Page();
        try
        {
            await annualClosingService.CloseAsync(UserId(), Workspace.CompanyId, SelectedYear,
                new ConfirmAnnualClosingCommand(CloseConfirmation.IndependentResultConfirmed), Now(), cancellationToken);
            StatusMessage = "Rok został zamknięty. PDF i roczny JPK_PKPIR zapisano w prywatnym archiwum.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (AnnualClosingBlockedException exception)
        {
            foreach (var blocker in exception.Blockers) ModelState.AddModelError(string.Empty, blocker.Message);
            return Page();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCorrectionAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(Correction));
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await annualClosingService.StartCorrectionAsync(
                UserId(), Workspace.CompanyId, SelectedYear, Correction.Reason, Now(), cancellationToken);
            StatusMessage = "Korekta roku została rozpoczęta. Możesz teraz korygować miesiące i dane roczne.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDownloadAsync(Guid fileId, int? year, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        var belongsToYear = Workspace.ClosingHistory.Any(item =>
            item.JpkStoredFileId == fileId || item.PdfStoredFileId == fileId
            || item.JpkReceiptStoredFileId == fileId);
        return belongsToYear ? Redirect($"/Files/{fileId}?download=true") : NotFound();
    }

    public async Task<IActionResult> OnPostApproveJpkAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(JpkAction));
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!Workspace.ClosingHistory.Any(item => item.Id == JpkAction.AnnualClosingId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await annualClosingService.ApproveJpkAsync(
                UserId(), JpkAction.AnnualClosingId, JpkAction.Reference, Now(), cancellationToken);
            StatusMessage = "Ta wersja rocznego JPK została zatwierdzona do ręcznego eksportu.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSentJpkAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(JpkAction));
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!Workspace.ClosingHistory.Any(item => item.Id == JpkAction.AnnualClosingId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await annualClosingService.MarkJpkSentAsync(
                UserId(), JpkAction.AnnualClosingId, JpkAction.Reference, Now(), cancellationToken);
            StatusMessage = "Zapisano ręczną wysyłkę. Firemka nie wysyłała pliku automatycznie.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostJpkOutcomeAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(JpkOutcome));
        if (!await LoadAsync(year, cancellationToken)) return NotFound();
        if (!Workspace.ClosingHistory.Any(item => item.Id == JpkOutcome.AnnualClosingId)) return NotFound();
        if (JpkOutcome.Receipt is null || JpkOutcome.Receipt.Length == 0)
            ModelState.AddModelError($"{nameof(JpkOutcome)}.{nameof(JpkOutcome.Receipt)}", "Dodaj plik potwierdzenia.");
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var content = JpkOutcome.Receipt!.OpenReadStream();
            await annualClosingService.RecordJpkOutcomeAsync(
                UserId(), JpkOutcome.AnnualClosingId, JpkOutcome.Result,
                new FilingReceiptUpload(JpkOutcome.Receipt.FileName, JpkOutcome.Receipt.ContentType, content),
                JpkOutcome.Reference, Now(), cancellationToken);
            StatusMessage = "Wynik i potwierdzenie przypięto do dokładnie tej wersji rocznego JPK.";
            return RedirectToPage(new { year = SelectedYear });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int? year, CancellationToken cancellationToken)
    {
        var company = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (company is null) return false;
        SelectedYear = year is >= 2000 and <= 2200 ? year.Value : company.TaxYears.Max(item => item.Year);
        try
        {
            Workspace = await annualClosingService.GetAsync(UserId(), company.Id, SelectedYear, Now(), cancellationToken);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private void KeepOnly(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }

    private string UserId() => RequestIdentity.GetRequiredUserId(User);
    private DateTimeOffset Now() => timeProvider.GetUtcNow();

    public sealed class DeclarationInput
    {
        [Display(Name = "Spis z natury na początek roku")]
        [Range(0, 999_999_999)]
        public decimal OpeningInventory { get; set; }

        [Display(Name = "Spis z natury na koniec roku")]
        [Range(0, 999_999_999)]
        public decimal ClosingInventory { get; set; }

        [Display(Name = "Zaliczki PIT faktycznie wpłacone")]
        [Range(0, 999_999_999)]
        public decimal PitAdvancesPaid { get; set; }

        [Display(Name = "Składki zdrowotne faktycznie wpłacone")]
        [Range(0, 999_999_999)]
        public decimal HealthContributionsPaid { get; set; }

        [Display(Name = "Dane wejściowe (spisy z natury i wpłaty) zostały niezależnie sprawdzone")]
        public bool IndependentVerificationConfirmed { get; set; }

        [Display(Name = "Dowód sprawdzenia")]
        [Required, StringLength(2_000)]
        public string EvidenceReference { get; set; } = string.Empty;

        [Display(Name = "Data sprawdzenia")]
        [Required]
        public DateOnly? ConfirmedOn { get; set; }
    }

    public sealed class CorrectionInput
    {
        [Display(Name = "Powód korekty")]
        [Required, StringLength(2_000)]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class CloseInput
    {
        [Display(Name = "Porównałem roczne podsumowanie z niezależnie sprawdzonym wynikiem")]
        public bool IndependentResultConfirmed { get; set; }
    }

    public sealed class JpkActionInput
    {
        [Required]
        public Guid AnnualClosingId { get; set; }

        [Display(Name = "Opis kontroli lub referencja ręcznej wysyłki")]
        [Required, StringLength(2_000)]
        public string Reference { get; set; } = string.Empty;
    }

    public sealed class JpkOutcomeInput
    {
        [Required]
        public Guid AnnualClosingId { get; set; }

        [Display(Name = "Wynik")]
        public FilingSubmissionOutcome Result { get; set; }

        [Display(Name = "Plik UPO lub potwierdzenia")]
        public IFormFile? Receipt { get; set; }

        [Display(Name = "Numer UPO albo opis odrzucenia")]
        [Required, StringLength(2_000)]
        public string Reference { get; set; } = string.Empty;
    }
}
