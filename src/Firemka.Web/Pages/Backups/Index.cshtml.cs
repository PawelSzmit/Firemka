using Firemka.Application.Backups;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Firemka.Web.Pages.Backups;

[Authorize]
public sealed class IndexModel(IBackupService service, TimeProvider timeProvider) : PageModel
{
    public BackupOverview Overview { get; private set; } = null!;
    public string? NewToken { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Overview = await service.GetOverviewAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);

    public async Task<IActionResult> OnPostGenerateTokenAsync(CancellationToken cancellationToken)
    {
        var issued = await service.IssueTokenAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);
        NewToken = issued.RawToken;
        Overview = await service.GetOverviewAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeTokenAsync(CancellationToken cancellationToken)
    {
        await service.RevokeTokenAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);
        StatusMessage = "Token klienta Mac został unieważniony.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRequestNowAsync(CancellationToken cancellationToken)
    {
        var overview = await service.GetOverviewAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);
        if (!overview.IsConfigured)
        {
            StatusMessage = "Najpierw wygeneruj i zapisz token w Pęku kluczy Maca.";
            return RedirectToPage();
        }
        await service.RequestNowAsync(UserId(), timeProvider.GetUtcNow(), cancellationToken);
        StatusMessage = "Kopia została zlecona. Klient Mac pobierze ją przy najbliższym uruchomieniu.";
        return RedirectToPage();
    }

    private string UserId() => RequestIdentity.GetRequiredUserId(User);
}
