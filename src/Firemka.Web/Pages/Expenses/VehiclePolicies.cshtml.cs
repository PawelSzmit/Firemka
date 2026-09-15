using System.ComponentModel.DataAnnotations;
using Firemka.Application.Onboarding;
using Firemka.Application.Time;
using Firemka.Application.Vehicles;
using Firemka.Domain.Vehicles;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Expenses;

[Authorize]
public sealed class VehiclePoliciesModel(
    ICompanyProfileService companyProfileService,
    IVehiclePolicyService vehiclePolicyService,
    TimeProvider timeProvider) : PageModel
{
    public Guid CompanyId { get; private set; }
    public IReadOnlyList<VehiclePolicySnapshot> Policies { get; private set; } = [];

    [BindProperty]
    public VehicleDecisionInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostCreateAsync(VehicleCostKind kind, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        var today = PolishBusinessTime.Today(timeProvider);
        await vehiclePolicyService.CreatePendingAsync(
            RequestIdentity.GetRequiredUserId(User),
            new CreateVehiclePolicyCommand(CompanyId, kind, new DateOnly(today.Year, today.Month, 1)),
            timeProvider.GetUtcNow(),
            cancellationToken);
        StatusMessage = "Utworzono pustą politykę. Nadal wymaga ona dowodu i jawnych parametrów.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(Guid policyId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid || Input.VatDeductionPercent is null || Input.KpirCostPercent is null)
        {
            return Page();
        }

        try
        {
            await vehiclePolicyService.ActivateAsync(
                RequestIdentity.GetRequiredUserId(User),
                policyId,
                new ActivateVehiclePolicyCommand(
                    Input.VatDeductionPercent.Value,
                    Input.KpirCostPercent.Value,
                    Input.EvidenceReference),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Polityka została aktywowana z podanymi przez Ciebie parametrami.";
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRevisionAsync(
        Guid policyId,
        DateOnly effectiveFromMonth,
        CancellationToken cancellationToken)
    {
        try
        {
            await vehiclePolicyService.CreateRevisionAsync(
                RequestIdentity.GetRequiredUserId(User),
                policyId,
                effectiveFromMonth,
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Nowa wersja czeka na osobne potwierdzenie parametrów.";
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            if (!await LoadAsync(cancellationToken))
            {
                return NotFound();
            }

            return Page();
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var owner = RequestIdentity.GetRequiredUserId(User);
        var company = await companyProfileService.GetAsync(owner, cancellationToken);
        if (company is null)
        {
            return false;
        }

        CompanyId = company.Id;
        Policies = await vehiclePolicyService.ListAsync(owner, company.Id, cancellationToken);
        return true;
    }

    public sealed class VehicleDecisionInput
    {
        [Required(ErrorMessage = "Podaj procent odliczenia VAT.")]
        [Range(0, 100)]
        [Display(Name = "Odliczenie VAT (%)")]
        public decimal? VatDeductionPercent { get; set; }

        [Required(ErrorMessage = "Podaj procent kosztu KPiR.")]
        [Range(0, 100)]
        [Display(Name = "Koszt KPiR (%)")]
        public decimal? KpirCostPercent { get; set; }

        [Required(ErrorMessage = "Dodaj referencję do dokumentu lub potwierdzonej notatki.")]
        [StringLength(1000)]
        [Display(Name = "Dowód lub potwierdzona notatka")]
        public string EvidenceReference { get; set; } = string.Empty;
    }
}
