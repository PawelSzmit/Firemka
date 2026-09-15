using System.ComponentModel.DataAnnotations;
using Firemka.Application.Filings;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settings;

[Authorize]
public sealed class FilingsModel(
    ICompanyProfileService companyProfileService,
    IFilingService filingService,
    TimeProvider timeProvider) : PageModel
{
    public CompanyProfileSnapshot? Company { get; private set; }

    public FilingProfileSnapshot? CurrentProfile { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return RedirectToPage("/Setup/Company");
        }

        SetDefaults();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return RedirectToPage("/Setup/Company");
        }

        if (!Input.IndependentCheckConfirmed)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.IndependentCheckConfirmed)}",
                "Potwierdź niezależne sprawdzenie danych przed zapisaniem profilu.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await filingService.SaveProfileAsync(
                UserId(),
                Company!.Id,
                new SaveFilingProfileCommand(
                    Input.FirstName,
                    Input.LastName,
                    Input.BirthDate!.Value,
                    Input.Pesel,
                    Input.TaxOfficeCode,
                    Input.ZusInsuranceTitleCode,
                    Input.Email,
                    Input.ConfirmationEvidence,
                    Input.ConfirmedOn),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Potwierdzony profil urzędowy został zapisany jako nowa wersja.";
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Company = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (Company is null)
        {
            return false;
        }

        CurrentProfile = await filingService.GetProfileAsync(
            UserId(), Company.Id, cancellationToken);
        return true;
    }

    private void SetDefaults()
    {
        if (CurrentProfile is null)
        {
            Input.ConfirmedOn = PolishBusinessTime.Today(timeProvider);
            return;
        }

        Input = new InputModel
        {
            FirstName = CurrentProfile.FirstName,
            LastName = CurrentProfile.LastName,
            BirthDate = CurrentProfile.BirthDate,
            Pesel = CurrentProfile.Pesel,
            TaxOfficeCode = CurrentProfile.TaxOfficeCode,
            ZusInsuranceTitleCode = CurrentProfile.ZusInsuranceTitleCode,
            Email = CurrentProfile.Email,
            ConfirmationEvidence = CurrentProfile.ConfirmationEvidence,
            ConfirmedOn = CurrentProfile.ConfirmedOn,
        };
    }

    private string UserId() => RequestIdentity.GetRequiredUserId(User);

    public sealed class InputModel
    {
        [Display(Name = "Imię")]
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Nazwisko")]
        [Required]
        [StringLength(150)]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Data urodzenia")]
        [Required]
        public DateOnly? BirthDate { get; set; }

        [Display(Name = "PESEL")]
        [RegularExpression("^[0-9]{11}$", ErrorMessage = "PESEL musi mieć 11 cyfr.")]
        public string Pesel { get; set; } = string.Empty;

        [Display(Name = "Kod urzędu skarbowego")]
        [RegularExpression("^[0-9]{4}$", ErrorMessage = "Kod urzędu musi mieć 4 cyfry.")]
        public string TaxOfficeCode { get; set; } = string.Empty;

        [Display(Name = "Kod tytułu ubezpieczenia ZUS")]
        [RegularExpression("^[0-9]{4}$", ErrorMessage = "Kod tytułu ubezpieczenia musi mieć 4 cyfry.")]
        public string ZusInsuranceTitleCode { get; set; } = string.Empty;

        [Display(Name = "E-mail w dokumentach (opcjonalnie)")]
        [EmailAddress]
        [StringLength(254)]
        public string? Email { get; set; }

        [Display(Name = "Dowód sprawdzenia")]
        [Required]
        [StringLength(2_000)]
        public string ConfirmationEvidence { get; set; } = string.Empty;

        [Display(Name = "Data sprawdzenia")]
        [Required]
        public DateOnly ConfirmedOn { get; set; }

        [Display(Name = "Potwierdzam, że dane i kody sprawdzono w aktualnych źródłach urzędowych")]
        public bool IndependentCheckConfirmed { get; set; }
    }
}
