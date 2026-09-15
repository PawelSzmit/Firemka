using System.ComponentModel.DataAnnotations;
using Firemka.Application.Filings;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Domain.Filings;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settlements;

[Authorize]
public sealed class FilingsModel(
    ICompanyProfileService companyProfileService,
    IFilingService filingService,
    TimeProvider timeProvider) : PageModel
{
    public FilingWorkspace Workspace { get; private set; } = null!;
    public int SelectedYear { get; private set; }
    public int SelectedMonth { get; private set; }

    [BindProperty]
    public ActionInput Action { get; set; } = new();

    [BindProperty]
    public OutcomeInput Outcome { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, int? month, CancellationToken cancellationToken)
        => await LoadAsync(year, month, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostGenerateAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        try
        {
            await filingService.GenerateAsync(UserId(), Workspace.CompanyId, Workspace.Month, Now(), cancellationToken);
            StatusMessage = "Pliki zostały przygotowane i sprawdzone lokalnie względem zapisanych schematów.";
            return RedirectToSelectedMonth();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(Action));
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        if (!Workspace.Artifacts.Any(item => item.Id == Action.ArtifactId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await filingService.ApproveAsync(UserId(), Action.ArtifactId, Action.Reference, Now(), cancellationToken);
            StatusMessage = "Ta wersja pliku została zatwierdzona do ręcznego eksportu.";
            return RedirectToSelectedMonth();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostExportAsync(
        Guid artifactId,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        if (!Workspace.Artifacts.Any(item => item.Id == artifactId)) return NotFound();
        try
        {
            var artifact = await filingService.RecordExportAsync(UserId(), artifactId, Now(), cancellationToken);
            return Redirect($"/Files/{artifact.StoredFileId}?download=true");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSentAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(Action));
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        if (!Workspace.Artifacts.Any(item => item.Id == Action.ArtifactId)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await filingService.MarkSentAsync(UserId(), Action.ArtifactId, Action.Reference, Now(), cancellationToken);
            StatusMessage = "Zapisano informację o ręcznej wysyłce. Firemka niczego nie wysyłała automatycznie.";
            return RedirectToSelectedMonth();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostOutcomeAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        KeepOnly(nameof(Outcome));
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        if (!Workspace.Artifacts.Any(item => item.Id == Outcome.ArtifactId)) return NotFound();
        if (Outcome.Receipt is null || Outcome.Receipt.Length == 0)
        {
            ModelState.AddModelError($"{nameof(Outcome)}.{nameof(Outcome.Receipt)}", "Dodaj plik potwierdzenia.");
        }
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var content = Outcome.Receipt!.OpenReadStream();
            await filingService.RecordOutcomeAsync(
                UserId(),
                Outcome.ArtifactId,
                Outcome.Result,
                new FilingReceiptUpload(Outcome.Receipt.FileName, Outcome.Receipt.ContentType, content),
                Outcome.Reference,
                Now(),
                cancellationToken);
            StatusMessage = "Wynik i potwierdzenie zostały przypięte do dokładnie tej wersji pliku.";
            return RedirectToSelectedMonth();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        var company = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (company is null) return false;
        var current = PolishBusinessTime.Today(timeProvider);
        SelectedYear = year ?? current.Year;
        SelectedMonth = month is >= 1 and <= 12 ? month.Value : current.Month;
        Workspace = await filingService.GetAsync(
            UserId(), company.Id, new DateOnly(SelectedYear, SelectedMonth, 1), cancellationToken);
        return true;
    }

    private void KeepOnly(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray())
        {
            ModelState.Remove(key);
        }
    }

    private IActionResult RedirectToSelectedMonth()
        => RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
    private string UserId() => RequestIdentity.GetRequiredUserId(User);
    private DateTimeOffset Now() => timeProvider.GetUtcNow();

    public sealed class ActionInput
    {
        [Required]
        public Guid ArtifactId { get; set; }

        [Display(Name = "Opis kontroli lub referencja ręcznej wysyłki")]
        [Required]
        [StringLength(2_000)]
        public string Reference { get; set; } = string.Empty;
    }

    public sealed class OutcomeInput
    {
        [Required]
        public Guid ArtifactId { get; set; }

        [Display(Name = "Wynik")]
        public FilingSubmissionOutcome Result { get; set; }

        [Display(Name = "Plik UPO lub potwierdzenia")]
        public IFormFile? Receipt { get; set; }

        [Display(Name = "Numer UPO albo opis odrzucenia")]
        [Required]
        [StringLength(2_000)]
        public string Reference { get; set; } = string.Empty;
    }
}
