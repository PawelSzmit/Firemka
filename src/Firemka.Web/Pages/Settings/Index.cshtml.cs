using System.ComponentModel.DataAnnotations;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Domain.Companies;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Settings;

[Authorize]
public sealed class IndexModel(ICompanyProfileService companyProfileService, TimeProvider timeProvider) : PageModel
{
    public CompanyProfileSnapshot? Profile { get; private set; }

    [BindProperty]
    public SubscriptionInput Subscription { get; set; } = new();

    [BindProperty]
    public EnergyInput Energy { get; set; } = new();

    [BindProperty]
    public VatInput Vat { get; set; } = new();

    [BindProperty]
    public ZusInput Zus { get; set; } = new();

    [BindProperty]
    public VehicleInput Vehicle { get; set; } = new();

    [BindProperty]
    public TaxYearInput NewTaxYear { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        SetDefaults();
    }

    public async Task<IActionResult> OnPostSubscriptionAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Subscription));
        ValidateFirstDayOfMonth(
            $"{nameof(Subscription)}.{nameof(SubscriptionInput.ValidFromMonth)}",
            Subscription.ValidFromMonth);
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Subscription));
            return Page();
        }

        try
        {
            await companyProfileService.ChangeSubscriptionRateAsync(
                UserId(),
                Subscription.ValidFromMonth,
                Subscription.NetMonthlyAmount,
                Subscription.VatRate,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Subscription));
            return Page();
        }

        TempData["SettingsMessage"] = "Nowa stawka abonamentu została zapisana od wybranego miesiąca.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEnergyAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Energy));
        ValidateFirstDayOfMonth(
            $"{nameof(Energy)}.{nameof(EnergyInput.ValidFromMonth)}",
            Energy.ValidFromMonth);
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Energy));
            return Page();
        }

        try
        {
            await companyProfileService.ChangeEnergyRateAsync(
                UserId(),
                Energy.ValidFromMonth,
                Energy.GrossPricePerKwh,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Energy));
            return Page();
        }

        TempData["SettingsMessage"] = "Nowa cena energii została zapisana od wybranego miesiąca.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostOpenYearAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(NewTaxYear));
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(NewTaxYear));
            return Page();
        }

        try
        {
            await companyProfileService.OpenTaxYearAsync(
                UserId(),
                NewTaxYear.Year,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(NewTaxYear));
            return Page();
        }

        TempData["SettingsMessage"] = $"Rok {NewTaxYear.Year} został otwarty na skali podatkowej.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVatAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Vat));
        ValidateFirstDayOfMonth($"{nameof(Vat)}.{nameof(VatInput.ValidFromMonth)}", Vat.ValidFromMonth);
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Vat));
            return Page();
        }

        try
        {
            await companyProfileService.ChangeVatProfileAsync(
                UserId(), Vat.ValidFromMonth, Vat.Profile, timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Vat));
            return Page();
        }

        TempData["SettingsMessage"] = "Nowe ustawienie VAT zostało zapisane od wybranego miesiąca.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostZusAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Zus));
        ValidateFirstDayOfMonth($"{nameof(Zus)}.{nameof(ZusInput.ValidFromMonth)}", Zus.ValidFromMonth);
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Zus));
            return Page();
        }

        try
        {
            await companyProfileService.ChangeZusProfileAsync(
                UserId(), Zus.ValidFromMonth, Zus.Profile, timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Zus));
            return Page();
        }

        TempData["SettingsMessage"] = "Nowe ustawienie ZUS zostało zapisane od wybranego miesiąca.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVehicleAsync(CancellationToken cancellationToken)
    {
        KeepOnlyModelState(nameof(Vehicle));
        ValidateFirstDayOfMonth(
            $"{nameof(Vehicle)}.{nameof(VehicleInput.ValidFromMonth)}",
            Vehicle.ValidFromMonth);
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Vehicle));
            return Page();
        }

        try
        {
            await companyProfileService.ChangeVehicleProfileAsync(
                UserId(),
                Vehicle.ValidFromMonth,
                Vehicle.Arrangement,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync(cancellationToken);
            SetDefaults(nameof(Vehicle));
            return Page();
        }

        TempData["SettingsMessage"] = "Nowe ustawienie samochodu zostało zapisane od wybranego miesiąca.";
        return RedirectToPage();
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

    private void ValidateFirstDayOfMonth(string key, DateOnly validFromMonth)
    {
        if (validFromMonth != default && validFromMonth.Day != 1)
        {
            ModelState.AddModelError(key, "Nowy okres musi zaczynać się pierwszego dnia miesiąca.");
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
        => Profile = await companyProfileService.GetAsync(UserId(), cancellationToken);

    private void SetDefaults(string? preservedForm = null)
    {
        if (Profile is null)
        {
            return;
        }

        var today = PolishBusinessTime.Today(timeProvider);
        var currentSubscription = Profile.SubscriptionRates.Last();
        var currentEnergy = Profile.EnergyRates.Last();
        var currentVat = Profile.VatProfiles.Last();
        var currentZus = Profile.ZusProfiles.Last();
        var currentVehicle = Profile.VehicleProfiles.Last();
        if (preservedForm != nameof(Subscription))
        {
            Subscription = new SubscriptionInput
            {
                ValidFromMonth = PolishBusinessTime.NextPeriodStart(today, currentSubscription.ValidFromMonth),
                NetMonthlyAmount = currentSubscription.NetMonthlyAmount,
                VatRate = currentSubscription.VatRate,
            };
        }

        if (preservedForm != nameof(Energy))
        {
            Energy = new EnergyInput
            {
                ValidFromMonth = PolishBusinessTime.NextPeriodStart(today, currentEnergy.ValidFromMonth),
                GrossPricePerKwh = currentEnergy.GrossPricePerKwh,
            };
        }

        if (preservedForm != nameof(NewTaxYear))
        {
            NewTaxYear = new TaxYearInput { Year = Profile.TaxYears.Max(item => item.Year) + 1 };
        }


        if (preservedForm != nameof(Vat))
        {
            Vat = new VatInput
            {
                ValidFromMonth = PolishBusinessTime.NextPeriodStart(today, currentVat.ValidFromMonth),
                Profile = currentVat.Profile,
            };
        }

        if (preservedForm != nameof(Zus))
        {
            Zus = new ZusInput
            {
                ValidFromMonth = PolishBusinessTime.NextPeriodStart(today, currentZus.ValidFromMonth),
                Profile = currentZus.Profile,
            };
        }

        if (preservedForm != nameof(Vehicle))
        {
            Vehicle = new VehicleInput
            {
                ValidFromMonth = PolishBusinessTime.NextPeriodStart(today, currentVehicle.ValidFromMonth),
                Arrangement = currentVehicle.Arrangement,
            };
        }
    }

    public sealed class SubscriptionInput
    {
        [Display(Name = "Obowiązuje od miesiąca")]
        public DateOnly ValidFromMonth { get; set; }

        [Display(Name = "Kwota netto")]
        [Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true)]
        public decimal NetMonthlyAmount { get; set; }

        [Display(Name = "VAT (%)")]
        [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
        public decimal VatRate { get; set; }
    }

    public sealed class EnergyInput
    {
        [Display(Name = "Obowiązuje od miesiąca")]
        public DateOnly ValidFromMonth { get; set; }

        [Display(Name = "Cena brutto za kWh")]
        [Range(typeof(decimal), "0.0001", "9999", ParseLimitsInInvariantCulture = true)]
        public decimal GrossPricePerKwh { get; set; }
    }

    public sealed class TaxYearInput
    {
        [Display(Name = "Nowy rok")]
        [Range(2000, 2200)]
        public int Year { get; set; }
    }


    public sealed class VatInput
    {
        [Display(Name = "Obowiązuje od miesiąca")]
        public DateOnly ValidFromMonth { get; set; }

        [Display(Name = "Status VAT")]
        public VatProfile Profile { get; set; } = VatProfile.ActiveMonthly;
    }

    public sealed class ZusInput
    {
        [Display(Name = "Obowiązuje od miesiąca")]
        public DateOnly ValidFromMonth { get; set; }

        [Display(Name = "Zakres składek")]
        public ZusProfile Profile { get; set; } = ZusProfile.HealthOnlyDueToEmployment;
    }

    public sealed class VehicleInput
    {
        [Display(Name = "Obowiązuje od miesiąca")]
        public DateOnly ValidFromMonth { get; set; }

        [Display(Name = "Samochód w działalności")]
        public VehicleArrangement Arrangement { get; set; } = VehicleArrangement.None;
    }
}
