using Firemka.Application.Backups;
using Firemka.Domain.AnnualClosing;
using Firemka.Infrastructure.Backups;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class BackupServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T10:00:00Z");

    [Fact]
    public async Task Raw_token_is_returned_once_and_revocation_blocks_backup_authorization()
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);

        Assert.StartsWith($"fmbk_{issued.Prefix}_", issued.RawToken, StringComparison.Ordinal);
        Assert.DoesNotContain(issued.RawToken, (await db.BackupAccessTokens.SingleAsync()).SecretSha256);
        Assert.Null(await service.AuthorizeAsync("wrong-token", Now));
        Assert.Equal("owner-1", (await service.AuthorizeAsync(issued.RawToken, Now))!.OwnerUserId);

        await service.RevokeTokenAsync("owner-1", Now.AddMinutes(1));
        Assert.Null(await service.AuthorizeAsync(issued.RawToken, Now.AddMinutes(2)));
    }

    [Fact]
    public async Task Plan_catches_up_daily_manual_and_annual_requests_and_success_completes_them()
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);
        var authorization = (await service.AuthorizeAsync(issued.RawToken, Now))!;
        var manualId = await service.RequestNowAsync("owner-1", Now);
        var annual = AnnualArchiveRequest.Stage(Guid.NewGuid(), "owner-1", 2026, Guid.NewGuid(), Now);
        db.AnnualArchiveRequests.Add(annual);
        await db.SaveChangesAsync();

        var plan = await service.GetPlanAsync(authorization, Now);
        Assert.True(plan.DailyRequired);
        Assert.Equal(manualId, plan.ManualRequestId);
        Assert.Equal(2026, Assert.Single(plan.AnnualArchives).TaxYear);

        await service.ReportAsync(authorization, new BackupReportCommand(
            true, "firemka-backup-20260914T100500000Z-v1.fmbak", new string('A', 64), 1, null,
            manualId, null), Now.AddMinutes(5));
        var afterDaily = await service.GetPlanAsync(authorization, Now.AddHours(1));
        Assert.False(afterDaily.DailyRequired);
        Assert.Single(afterDaily.AnnualArchives);

        await service.ReportAsync(authorization, new BackupReportCommand(
            true, "firemka-archive-2026-20260914T100600000Z-v1.fmbak", new string('B', 64), 1, null,
            null, annual.Id), Now.AddMinutes(6));
        var after = await service.GetPlanAsync(authorization, Now.AddHours(1));
        Assert.False(after.DailyRequired);
        Assert.Empty(after.AnnualArchives);
        Assert.True((await service.GetPlanAsync(authorization, Now.AddDays(1))).DailyRequired);
        Assert.NotNull((await service.GetOverviewAsync("owner-1", Now.AddHours(1))).LastSuccessfulAtUtc);
    }

    [Fact]
    public async Task Report_cannot_complete_another_owners_request()
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);
        var authorization = (await service.AuthorizeAsync(issued.RawToken, Now))!;
        var foreign = await service.RequestNowAsync("owner-2", Now);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReportAsync(
            authorization,
            new BackupReportCommand(true, "backup.fmbak", new string('B', 64), 1, null, foreign, null),
            Now.AddMinutes(1)));
    }

    [Fact]
    public async Task Successful_report_rejects_two_request_kinds_without_completing_either()
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);
        var authorization = (await service.AuthorizeAsync(issued.RawToken, Now))!;
        var manualId = await service.RequestNowAsync("owner-1", Now);
        var annual = AnnualArchiveRequest.Stage(Guid.NewGuid(), "owner-1", 2026, Guid.NewGuid(), Now);
        db.AnnualArchiveRequests.Add(annual);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync(
            authorization,
            new BackupReportCommand(
                true, "firemka-backup-20260914T100000000Z-v1.fmbak", new string('A', 64), 1,
                null, manualId, annual.Id),
            Now.AddMinutes(1)));

        Assert.Null((await db.BackupRequests.SingleAsync()).CompletedAtUtc);
        Assert.Null((await db.AnnualArchiveRequests.SingleAsync()).CompletedAtUtc);
    }

    [Theory]
    [InlineData("firemka-backup-20260914T100000000Z-v1.fmbak", 1)]
    [InlineData("firemka-archive-2025-20260914T100000000Z-v1.fmbak", 1)]
    [InlineData("firemka-archive-2026-20260914T100000000Z-v2.fmbak", 2)]
    public async Task Annual_report_requires_matching_year_name_and_supported_format(
        string fileName,
        int formatVersion)
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);
        var authorization = (await service.AuthorizeAsync(issued.RawToken, Now))!;
        var annual = AnnualArchiveRequest.Stage(Guid.NewGuid(), "owner-1", 2026, Guid.NewGuid(), Now);
        db.AnnualArchiveRequests.Add(annual);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportAsync(
            authorization,
            new BackupReportCommand(
                true, fileName, new string('A', 64), formatVersion, null, null, annual.Id),
            Now.AddMinutes(1)));

        Assert.Null((await db.AnnualArchiveRequests.SingleAsync()).CompletedAtUtc);
    }

    [Fact]
    public async Task Annual_report_with_matching_year_and_format_completes_request()
    {
        await using var db = CreateDb();
        var service = new BackupService(db);
        var issued = await service.IssueTokenAsync("owner-1", Now);
        var authorization = (await service.AuthorizeAsync(issued.RawToken, Now))!;
        var annual = AnnualArchiveRequest.Stage(Guid.NewGuid(), "owner-1", 2026, Guid.NewGuid(), Now);
        db.AnnualArchiveRequests.Add(annual);
        await db.SaveChangesAsync();

        await service.ReportAsync(
            authorization,
            new BackupReportCommand(
                true, "firemka-archive-2026-20260914T100000000Z-v1.fmbak", new string('A', 64), 1,
                null, null, annual.Id),
            Now.AddMinutes(1));

        Assert.NotNull((await db.AnnualArchiveRequests.SingleAsync()).CompletedAtUtc);
    }

    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phase11-service-{Guid.NewGuid():N}").Options);
}
