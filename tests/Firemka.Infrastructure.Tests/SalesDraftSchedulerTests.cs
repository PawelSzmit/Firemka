using Firemka.Application.ExternalServices;
using Firemka.Domain.Companies;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Jobs;
using Firemka.Infrastructure.Ksef.Outgoing;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class SalesDraftSchedulerTests
{
    [Fact]
    public async Task Scheduler_always_queues_the_monthly_draft_but_never_enables_automation()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var scheduler = new SalesDraftScheduler(
            db,
            new BackgroundJobQueue(db),
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = true });

        await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-01T00:05:00Z"));
        await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-01T00:06:00Z"));

        var job = Assert.Single(await db.BackgroundJobs.ToListAsync());
        Assert.Equal(SalesDraftJobHandler.JobTypeName, job.JobType);
        Assert.Contains(company.Id.ToString("N"), job.IdempotencyKey);
        Assert.Empty(await db.SalesAutomationSettings.ToListAsync());
    }

    [Fact]
    public async Task Automatic_issue_is_queued_only_on_the_first_day_with_owner_and_operator_consent()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var settings = SalesAutomationSettings.CreateDisabled(
            company.Id,
            company.OwnerUserId,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        settings.Enable(true, DateTimeOffset.Parse("2026-09-30T10:00:00Z"));
        db.SalesAutomationSettings.Add(settings);
        await db.SaveChangesAsync();
        var scheduler = new SalesDraftScheduler(
            db,
            new BackgroundJobQueue(db),
            new KsefOutgoingOptions
            {
                Enabled = true,
                AdapterConfigured = true,
                Environment = KsefEnvironment.Test,
                AllowAutomation = true,
            });

        await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-01T00:05:00Z"));

        var jobs = await db.BackgroundJobs.OrderBy(item => item.JobType).ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, item => item.JobType == SalesDraftJobHandler.JobTypeName);
        var issueJob = Assert.Single(jobs, item => item.JobType == SalesInvoiceIssueJobHandler.JobTypeName);
        Assert.Equal(SalesInvoiceIssueJobHandler.StatusPollingMaximumAttempts, issueJob.MaximumAttempts);
    }

    [Fact]
    public async Task Operator_gate_prevents_automatic_issue_even_when_old_owner_setting_is_enabled()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var settings = SalesAutomationSettings.CreateDisabled(
            company.Id,
            company.OwnerUserId,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        settings.Enable(true, DateTimeOffset.Parse("2026-09-30T10:00:00Z"));
        db.SalesAutomationSettings.Add(settings);
        await db.SaveChangesAsync();
        var scheduler = new SalesDraftScheduler(
            db,
            new BackgroundJobQueue(db),
            new KsefOutgoingOptions { Enabled = true, AdapterConfigured = true, AllowAutomation = false });

        await scheduler.EnqueueDueAsync(DateTimeOffset.Parse("2026-10-01T00:05:00Z"));

        var job = Assert.Single(await db.BackgroundJobs.ToListAsync());
        Assert.Equal(SalesDraftJobHandler.JobTypeName, job.JobType);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"firemka-sales-scheduler-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Company> SeedCompanyAsync(AppDbContext db)
    {
        var company = Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowa 2, 00-002 Warszawa",
            new DateOnly(2026, 9, 15),
            "Testowy Klient",
            "1234563218",
            "Testowa 1, 00-001 Warszawa",
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }
}
