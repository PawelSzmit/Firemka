using System.ComponentModel.DataAnnotations;
using Firemka.Application.Onboarding;
using Firemka.Domain.Companies;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Setup;

[Authorize]
public sealed class CompanyModel(ICompanyProfileService companyProfileService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (await companyProfileService.ExistsAsync(
            RequestIdentity.GetRequiredUserId(User),
            cancellationToken))
        {
            return RedirectToPage("/Settings/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userId = RequestIdentity.GetRequiredUserId(User);
        if (await companyProfileService.ExistsAsync(userId, cancellationToken))
        {
            return RedirectToPage("/Settings/Index");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await companyProfileService.RegisterAsync(
            new RegisterCompanyCommand(
                userId,
                Input.CompanyName,
                Input.CompanyNip,
                Input.CompanyAddress,
                Input.BusinessStartDate!.Value,
                Input.CounterpartyName,
                Input.CounterpartyNip,
                Input.CounterpartyAddress,
                Input.ServiceDescription,
                Input.SubscriptionNetMonthlyAmount!.Value,
                Input.SubscriptionVatRate,
                Input.EnergyGrossPricePerKwh,
                Input.VehicleArrangement,
                DateTimeOffset.UtcNow),
            cancellationToken);
        return RedirectToPage("/Month/Index");
    }

    public sealed class InputModel
    {
        [Display(Name = "Nazwa firmy")]
        [Required(ErrorMessage = "Podaj nazwę firmy.")]
        [StringLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "NIP firmy")]
        [Required(ErrorMessage = "Podaj NIP firmy.")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "NIP musi zawierać 10 cyfr.")]
        public string CompanyNip { get; set; } = string.Empty;

        [Display(Name = "Adres firmy")]
        [Required(ErrorMessage = "Podaj adres firmy.")]
        [StringLength(500)]
        public string CompanyAddress { get; set; } = string.Empty;

        [Display(Name = "Data rozpoczęcia działalności")]
        [Required(ErrorMessage = "Podaj datę rozpoczęcia działalności.")]
        public DateOnly? BusinessStartDate { get; set; }

        [Display(Name = "Nazwa klienta")]
        [Required(ErrorMessage = "Podaj nazwę klienta.")]
        [StringLength(200)]
        public string CounterpartyName { get; set; } = string.Empty;

        [Display(Name = "NIP klienta")]
        [Required(ErrorMessage = "Podaj NIP klienta.")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "NIP musi zawierać 10 cyfr.")]
        public string CounterpartyNip { get; set; } = string.Empty;

        [Display(Name = "Adres klienta")]
        [Required(ErrorMessage = "Podaj adres klienta.")]
        [StringLength(500)]
        public string CounterpartyAddress { get; set; } = string.Empty;

        [Display(Name = "Opis usługi na fakturze")]
        [Required(ErrorMessage = "Podaj opis usługi.")]
        [StringLength(500)]
        public string ServiceDescription { get; set; } = string.Empty;

        [Display(Name = "Miesięczny abonament netto")]
        [Required(ErrorMessage = "Podaj kwotę abonamentu.")]
        [Range(
            typeof(decimal),
            "0.01",
            "999999999.99",
            ErrorMessage = "Podaj dodatnią kwotę abonamentu.",
            ParseLimitsInInvariantCulture = true)]
        public decimal? SubscriptionNetMonthlyAmount { get; set; }

        [Display(Name = "Stawka VAT abonamentu (%)")]
        [Range(
            typeof(decimal),
            "0",
            "100",
            ErrorMessage = "Stawka VAT musi mieścić się od 0 do 100%.",
            ParseLimitsInInvariantCulture = true)]
        public decimal SubscriptionVatRate { get; set; } = 23m;

        [Display(Name = "Cena energii brutto za kWh")]
        [Range(
            typeof(decimal),
            "0.0001",
            "9999",
            ErrorMessage = "Podaj dodatnią cenę energii.",
            ParseLimitsInInvariantCulture = true)]
        public decimal EnergyGrossPricePerKwh { get; set; } = 0.91m;

        [Display(Name = "Samochód w działalności")]
        public VehicleArrangement VehicleArrangement { get; set; } = VehicleArrangement.None;
    }
}
