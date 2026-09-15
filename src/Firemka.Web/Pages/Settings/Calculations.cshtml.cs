using System.ComponentModel.DataAnnotations;
using Firemka.Application.MonthClosing;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Domain.Calculations;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settings;

[Authorize]
public sealed class CalculationsModel(
    ICompanyProfileService companyProfileService,
    IMonthClosingService monthClosingService,
    TimeProvider timeProvider) : PageModel
{
    public CompanyProfileSnapshot? Company { get; private set; }

    public CalculationRuleSetSnapshot? RuleSet { get; private set; }

    public int SelectedYear { get; private set; }

    [BindProperty]
    public ConfirmationInput Confirmation { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, cancellationToken))
        {
            return Page();
        }

        Confirmation.ConfirmedOn = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                timeProvider.GetUtcNow(),
                "Europe/Warsaw").DateTime);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int? year, CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Confirmation));
        if (!Confirmation.IndependentVerificationConfirmed)
        {
            ModelState.AddModelError(
                nameof(Confirmation) + "." + nameof(Confirmation.IndependentVerificationConfirmed),
                "Zaznacz niezależną weryfikację.");
        }

        if (!await LoadAsync(year, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Najpierw skonfiguruj firmę.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (RuleSet is null)
        {
            ModelState.AddModelError(string.Empty, $"Brakuje zestawu zasad dla roku {SelectedYear}.");
            return Page();
        }

        if (RuleSet.Trust == CalculationRuleTrust.IndependentlyConfirmed)
        {
            StatusMessage = "Zasady dla tego roku są już niezależnie potwierdzone.";
            return RedirectToPage(new { year = SelectedYear });
        }

        try
        {
            var confirmed = await monthClosingService.ConfirmRuleSetAsync(
                UserId(),
                RuleSet.Id,
                new ConfirmCalculationRuleSetCommand(
                    Confirmation.IndependentVerificationConfirmed,
                    Confirmation.EvidenceReference,
                    Confirmation.ConfirmedOn!.Value),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = $"Zasady zapisano jako niezależnie potwierdzoną wersję {confirmed.VersionNumber}.";
            return RedirectToPage(new { year = SelectedYear });
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

    private async Task<bool> LoadAsync(int? year, CancellationToken cancellationToken)
    {
        SelectedYear = year is >= 2000 and <= 2200
            ? year.Value
            : PolishBusinessTime.Today(timeProvider).Year;
        Company = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (Company is null)
        {
            RuleSet = null;
            return false;
        }

        var referenceMonth = Company.BusinessStartDate.Year == SelectedYear
            ? new DateOnly(SelectedYear, Company.BusinessStartDate.Month, 1)
            : new DateOnly(SelectedYear, 1, 1);
        var closing = await monthClosingService.GetAsync(
            UserId(),
            Company.Id,
            referenceMonth,
            timeProvider.GetUtcNow(),
            cancellationToken);
        RuleSet = closing?.RuleSet;
        return true;
    }

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

    public sealed class ConfirmationInput
    {
        [Display(Name = "Potwierdzam, że mam niezależną weryfikację")]
        public bool IndependentVerificationConfirmed { get; set; }

        [Required(ErrorMessage = "Podaj odniesienie do niezależnej weryfikacji.")]
        [StringLength(2_000)]
        [Display(Name = "Odniesienie do opinii lub sprawdzenia")]
        public string EvidenceReference { get; set; } = string.Empty;

        [Required(ErrorMessage = "Podaj datę niezależnej weryfikacji.")]
        [DataType(DataType.Date)]
        [Display(Name = "Data sprawdzenia")]
        public DateOnly? ConfirmedOn { get; set; }
    }
}
