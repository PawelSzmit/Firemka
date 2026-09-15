using System.ComponentModel.DataAnnotations;
using Firemka.Application.Onboarding;
using Firemka.Application.Payments;
using Firemka.Application.Time;
using Firemka.Domain.Payments;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Payments;

[Authorize]
public sealed class IndexModel(
    ICompanyProfileService companyProfileService,
    IPaymentService paymentService,
    TimeProvider timeProvider) : PageModel
{
    public PaymentWorkspace Workspace { get; private set; } = null!;
    public int SelectedYear { get; private set; }
    public int SelectedMonth { get; private set; }

    [BindProperty]
    public PaymentInput Payment { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        Payment.PaidOn = PolishBusinessTime.Today(timeProvider);
        return Page();
    }

    public async Task<IActionResult> OnPostPaidAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(year, month, cancellationToken)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await paymentService.RecordFullAsync(
                UserId(),
                Workspace.CompanyId,
                Workspace.Month,
                new RecordFullPaymentCommand(
                    Payment.Kind,
                    Payment.TargetId,
                    Payment.TargetVersion,
                    Payment.AmountPaid,
                    Payment.PaidOn!.Value,
                    Payment.ExternalIdentifier),
                timeProvider.GetUtcNow(),
                cancellationToken);
            StatusMessage = "Pełna płatność została zapisana przy dokładnie tej wersji należności.";
            return RedirectToPage(new { year = SelectedYear, month = SelectedMonth });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int? year, int? month, CancellationToken cancellationToken)
    {
        var company = await companyProfileService.GetAsync(UserId(), cancellationToken);
        if (company is null) return false;
        var today = PolishBusinessTime.Today(timeProvider);
        SelectedYear = year is >= 2000 and <= 2200 ? year.Value : today.Year;
        SelectedMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        Workspace = await paymentService.GetAsync(
            UserId(), company.Id, new DateOnly(SelectedYear, SelectedMonth, 1), cancellationToken);
        return true;
    }

    private string UserId() => RequestIdentity.GetRequiredUserId(User);

    public sealed class PaymentInput
    {
        public PaymentKind Kind { get; set; }
        public Guid TargetId { get; set; }
        public int TargetVersion { get; set; }

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal AmountPaid { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly? PaidOn { get; set; }

        [Required(ErrorMessage = "Wpisz unikalną referencję przelewu.")]
        [StringLength(500)]
        public string ExternalIdentifier { get; set; } = string.Empty;
    }
}
