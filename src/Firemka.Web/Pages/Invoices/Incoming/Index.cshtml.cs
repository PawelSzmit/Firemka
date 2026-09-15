using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Firemka.Application.Documents;
using Firemka.Application.Jobs;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Ksef.Incoming;
using Firemka.Infrastructure.Persistence;
using Firemka.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Web.Pages.Invoices.Incoming;

[Authorize]
public sealed class IndexModel(
    IIncomingDocumentService incomingDocumentService,
    IBackgroundJobQueue backgroundJobQueue,
    KsefIncomingOptions ksefOptions,
    AppDbContext dbContext) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Wybierz plik PDF, JPEG albo PNG.")]
    public IFormFile? Upload { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public SourceDocumentStatus? Status { get; set; }

    public IncomingDocumentPage Documents { get; private set; } = new([], 1, 25, 0);

    public bool IsKsefEnabled => ksefOptions.Enabled;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || Upload is null)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        try
        {
            await using var stream = Upload.OpenReadStream();
            var result = await incomingDocumentService.UploadAsync(
                RequestIdentity.GetRequiredUserId(User),
                new IncomingDocumentUpload(Upload.FileName, Upload.ContentType, stream),
                DateTimeOffset.UtcNow,
                cancellationToken);
            StatusMessage = "Dokument zapisano. Rozpoznawanie danych wykona lokalny worker.";
            return RedirectToPage("Details", new { id = result.DocumentId });
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(Upload), exception.Message);
            await LoadAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostSyncKsefAsync(CancellationToken cancellationToken)
    {
        if (!ksefOptions.Enabled)
        {
            ModelState.AddModelError(string.Empty, "Połączenie testowe KSeF nie jest jeszcze skonfigurowane.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var ownerUserId = RequestIdentity.GetRequiredUserId(User);
        var company = await dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (company is null)
        {
            ModelState.AddModelError(string.Empty, "Najpierw uzupełnij profil firmy.");
            await LoadAsync(cancellationToken);
            return Page();
        }

        var start = new DateTimeOffset(
            company.BusinessStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var now = DateTimeOffset.UtcNow;
        await backgroundJobQueue.EnqueueAsync(
            new BackgroundJobCommand(
                KsefSyncJobHandler.JobTypeName,
                JsonSerializer.Serialize(new KsefSyncJobPayload(ownerUserId, start)),
                $"ksef.incoming.sync:{ownerUserId}:{now:yyyyMMddHHmm}",
                now),
            cancellationToken);
        StatusMessage = "Synchronizacja KSeF została bezpiecznie zlecona.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Documents = await incomingDocumentService.ListAsync(
            RequestIdentity.GetRequiredUserId(User),
            new IncomingDocumentListQuery(PageNumber, 25, Status),
            cancellationToken);
    }
}
