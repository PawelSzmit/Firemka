using System.ComponentModel.DataAnnotations;
using Firemka.Application.Charging;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Expenses;

[Authorize]
public sealed class ChargingModel(
    ICompanyProfileService companyProfileService,
    IHomeChargingService homeChargingService,
    TimeProvider timeProvider) : PageModel
{
    public CompanyProfileSnapshot Company { get; private set; } = null!;
    public ChargingMonthSnapshot MonthData { get; private set; } = null!;
    public int SelectedYear { get; private set; }
    public int SelectedMonth { get; private set; }

    [BindProperty]
    public ProfileInput Profile { get; set; } = new();

    [BindProperty]
    public bool WhConfirmed { get; set; }

    [BindProperty]
    public IFormFile? CsvFile { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, int? month, CancellationToken cancellationToken)
        => await LoadAsync(year, month, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostSaveProfileAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid || Profile.Separator.Length != 1)
        {
            if (Profile.Separator.Length != 1)
            {
                ModelState.AddModelError("Profile.Separator", "Separator musi być jednym znakiem.");
            }

            return Page();
        }

        try
        {
            await homeChargingService.SaveProfileAsync(
                RequestIdentity.GetRequiredUserId(User),
                Company.Id,
                new SaveChargingProfileCommand(
                    Profile.Separator[0],
                    Profile.TimestampColumn,
                    Profile.EnergyWhColumn,
                    Profile.DateFormat,
                    Profile.TimeZoneId,
                    Profile.IdentityColumn),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Profil zapisano jako nieaktywny. Potwierdź jednostkę Wh osobnym krokiem.";
            return RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
        }
        catch (Exception exception) when (exception is ArgumentException or TimeZoneNotFoundException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostActivateProfileAsync(
        Guid profileId,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        try
        {
            await homeChargingService.ActivateProfileAsync(
                RequestIdentity.GetRequiredUserId(User),
                profileId,
                WhConfirmed,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Profil CSV jest aktywny. Jednostka Wh została jawnie potwierdzona.";
            return RedirectToPage(new { year, month });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(year, month, cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }
    }

    public async Task<IActionResult> OnPostImportAsync(
        Guid profileId,
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        if (CsvFile is null || CsvFile.Length == 0)
        {
            ModelState.AddModelError(nameof(CsvFile), "Wybierz plik CSV.");
            return Page();
        }

        try
        {
            await using var content = CsvFile.OpenReadStream();
            var result = await homeChargingService.ImportAsync(
                RequestIdentity.GetRequiredUserId(User),
                Company.Id,
                profileId,
                content,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = result.IsReplay
                ? $"Ten plik był już zaimportowany. Pominięto {result.SkippedRows} wierszy."
                : $"Import zakończony. Dodano {result.AddedRows}, pominięto {result.SkippedRows} wierszy.";
            return RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
        }
        catch (Exception exception) when (exception is ChargingCsvValidationException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostGenerateReportAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken))
        {
            return NotFound();
        }

        try
        {
            await homeChargingService.GenerateReportAsync(
                RequestIdentity.GetRequiredUserId(User),
                Company.Id,
                new DateOnly(SelectedYear, SelectedMonth, 1),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Zestawienie zapisano. Nie utworzono wpisu KPiR ani VAT.";
            return RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        var owner = RequestIdentity.GetRequiredUserId(User);
        var company = await companyProfileService.GetAsync(owner, cancellationToken);
        if (company is null)
        {
            return false;
        }

        var today = PolishBusinessTime.Today(timeProvider);
        SelectedYear = year is >= 2000 and <= 2200 ? year.Value : today.Year;
        SelectedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        Company = company;
        MonthData = (await homeChargingService.GetMonthAsync(
            owner,
            company.Id,
            new DateOnly(SelectedYear, SelectedMonth, 1),
            cancellationToken))!;
        if (MonthData.LatestProfile is null && string.IsNullOrWhiteSpace(Profile.TimestampColumn))
        {
            Profile = ProfileInput.FromHomeChargerDefaults();
        }

        return true;
    }

    public sealed class ProfileInput
    {
        [Required]
        [Display(Name = "Separator")]
        public string Separator { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Kolumna czasu")]
        public string TimestampColumn { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Kolumna energii Wh")]
        public string EnergyWhColumn { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Format daty")]
        public string DateFormat { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Strefa czasu")]
        public string TimeZoneId { get; set; } = string.Empty;

        [Display(Name = "Kolumna identyfikatora (opcjonalna)")]
        public string? IdentityColumn { get; set; }

        public static ProfileInput FromHomeChargerDefaults()
            => new()
            {
                Separator = ChargingCsvProfileDefaults.Separator.ToString(),
                TimestampColumn = ChargingCsvProfileDefaults.TimestampColumn,
                EnergyWhColumn = ChargingCsvProfileDefaults.EnergyWhColumn,
                DateFormat = ChargingCsvProfileDefaults.DateFormat,
                TimeZoneId = ChargingCsvProfileDefaults.TimeZoneId,
                IdentityColumn = ChargingCsvProfileDefaults.IdentityColumn,
            };
    }
}
