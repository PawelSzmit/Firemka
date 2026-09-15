using System.Text.Json;
using Firemka.Application.ExternalServices;
using Firemka.Application.Jobs;
using Firemka.Application.Sales;
using Firemka.Application.Time;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Sales;

public sealed class SalesDraftScheduler(
    AppDbContext dbContext,
    IBackgroundJobQueue backgroundJobQueue,
    KsefOutgoingOptions options)
{
    public async Task EnqueueDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var today = PolishBusinessTime.GetDate(nowUtc);
        var month = new DateOnly(today.Year, today.Month, 1);
        var companies = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.BusinessStartDate <= today)
            .Select(item => new { item.Id, item.OwnerUserId })
            .ToListAsync(cancellationToken);
        var automaticOwners = await dbContext.SalesAutomationSettings
            .AsNoTracking()
            .Where(item => item.Enabled)
            .Select(item => item.OwnerUserId)
            .ToHashSetAsync(cancellationToken);

        foreach (var company in companies)
        {
            var payload = JsonSerializer.Serialize(new SalesMonthJobPayload(company.OwnerUserId, month));
            await backgroundJobQueue.EnqueueAsync(
                new BackgroundJobCommand(
                    SalesDraftJobHandler.JobTypeName,
                    payload,
                    $"sales-draft:{company.Id:N}:{month:yyyyMM}",
                    nowUtc),
                cancellationToken);

            if (today.Day == 1
                && automaticOwners.Contains(company.OwnerUserId)
                && AutomationIsAllowed())
            {
                await backgroundJobQueue.EnqueueAsync(
                    new BackgroundJobCommand(
                    SalesInvoiceIssueJobHandler.JobTypeName,
                    payload,
                    $"sales-issue:{company.Id:N}:{month:yyyyMM}",
                    nowUtc,
                    SalesInvoiceIssueJobHandler.StatusPollingMaximumAttempts),
                    cancellationToken);
            }
        }
    }

    private bool AutomationIsAllowed()
        => options.Enabled
            && options.AdapterConfigured
            && options.AllowAutomation
            && (options.Environment != KsefEnvironment.Production || options.AllowProduction);
}

public sealed class SalesDraftJobHandler(
    ISalesInvoiceService salesInvoiceService,
    TimeProvider timeProvider) : IBackgroundJobHandler
{
    public const string JobTypeName = "sales.draft.ensure";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        var payload = Deserialize(job.PayloadJson);
        await salesInvoiceService.EnsureDraftAsync(
            payload.OwnerUserId,
            payload.ServiceMonth,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    internal static SalesMonthJobPayload Deserialize(string json)
        => JsonSerializer.Deserialize<SalesMonthJobPayload>(json)
            ?? throw new InvalidOperationException("Nieprawidłowe dane zadania faktury sprzedaży.");
}

public sealed class SalesInvoiceIssueJobHandler(
    ISalesInvoiceService salesInvoiceService,
    TimeProvider timeProvider) : IBackgroundJobHandler
{
    public const string JobTypeName = "sales.invoice.issue";
    public const int StatusPollingMaximumAttempts = 101;

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        LeasedBackgroundJob job,
        CancellationToken cancellationToken = default)
    {
        var payload = SalesDraftJobHandler.Deserialize(job.PayloadJson);
        var nowUtc = timeProvider.GetUtcNow();
        var today = PolishBusinessTime.GetDate(nowUtc);
        var invoice = await salesInvoiceService.EnsureDraftAsync(
            payload.OwnerUserId,
            payload.ServiceMonth,
            nowUtc,
            cancellationToken);
        var result = await salesInvoiceService.IssueAsync(
            payload.OwnerUserId,
            invoice.Id,
            automatic: true,
            today,
            nowUtc,
            cancellationToken);
        if (result.Status is SalesInvoiceStatus.Sending or SalesInvoiceStatus.DeliveryUncertain)
        {
            throw new InvalidOperationException(
                "Status faktury w KSeF nie jest jeszcze końcowy. Zadanie sprawdzi go ponownie bez ponownej wysyłki.");
        }
    }
}

public sealed record SalesMonthJobPayload(string OwnerUserId, DateOnly ServiceMonth);
