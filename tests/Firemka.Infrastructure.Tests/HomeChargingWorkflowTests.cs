using System.Text;
using Firemka.Application.Charging;
using Firemka.Domain.Charging;
using Firemka.Domain.Companies;
using Firemka.Infrastructure.Charging;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class HomeChargingWorkflowTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private static readonly DateOnly Month = new(2026, 9, 1);

    [Fact]
    public async Task Replayed_file_returns_existing_batch_and_overlap_adds_only_new_rows()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new HomeChargingService(db, new ChargingCsvParser());
        var profile = await ActiveProfileAsync(service, company.Id);

        var first = await service.ImportAsync("owner-1", company.Id, profile.Id, Csv(
            "2026-09-01 01:00;4000;A",
            "2026-09-01 02:00;6000;B"), Now);
        var replay = await service.ImportAsync("owner-1", company.Id, profile.Id, Csv(
            "2026-09-01 01:00;4000;A",
            "2026-09-01 02:00;6000;B"), Now.AddMinutes(1));
        var overlap = await service.ImportAsync("owner-1", company.Id, profile.Id, Csv(
            "2026-09-01 02:00;6000;B",
            "2026-09-02 02:00;1000;C"), Now.AddMinutes(2));

        Assert.Equal(first.BatchId, replay.BatchId);
        Assert.True(replay.IsReplay);
        Assert.Equal(0, replay.AddedRows);
        Assert.Equal(1, overlap.AddedRows);
        Assert.Equal(3, await db.ChargingSessions.CountAsync());
        Assert.Equal(2, await db.ChargingImportBatches.CountAsync());
    }

    [Fact]
    public async Task Report_uses_existing_month_rate_and_remains_unchanged_after_a_new_rate()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new HomeChargingService(db, new ChargingCsvParser());
        var profile = await ActiveProfileAsync(service, company.Id);
        await service.ImportAsync("owner-1", company.Id, profile.Id, Csv("2026-09-01 01:00;10000;A"), Now);

        var original = await service.GenerateReportAsync("owner-1", company.Id, Month, Now);
        company.ChangeEnergyRate(new DateOnly(2026, 10, 1), 1.10m, Now.AddDays(1));
        db.Entry(company.EnergyRates.Single(item => item.ValidFromMonth == new DateOnly(2026, 10, 1))).State = EntityState.Added;
        await db.SaveChangesAsync();
        var loaded = await service.GetReportAsync("owner-1", original.ReportId);

        Assert.NotNull(loaded);
        Assert.Equal(0.91m, loaded!.GrossRatePerKwh);
        Assert.Equal(10m, loaded.TotalKwh);
        Assert.Equal(9.10m, loaded.GrossCost);
        Assert.Equal(ChargingReport.TaxBasisUnconfirmed, loaded.TaxStatus);
    }

    [Fact]
    public async Task Same_report_input_is_idempotent_and_new_session_creates_version_two()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new HomeChargingService(db, new ChargingCsvParser());
        var profile = await ActiveProfileAsync(service, company.Id);
        await service.ImportAsync("owner-1", company.Id, profile.Id, Csv("2026-09-01 01:00;1000;A"), Now);

        var first = await service.GenerateReportAsync("owner-1", company.Id, Month, Now);
        var replay = await service.GenerateReportAsync("owner-1", company.Id, Month, Now.AddMinutes(1));
        await service.ImportAsync("owner-1", company.Id, profile.Id, Csv("2026-09-02 01:00;1000;B"), Now.AddMinutes(2));
        var second = await service.GenerateReportAsync("owner-1", company.Id, Month, Now.AddMinutes(3));

        Assert.Equal(first.ReportId, replay.ReportId);
        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.ReportId, second.PreviousReportId);
        Assert.Equal(2, await db.ChargingReports.CountAsync());
    }

    [Fact]
    public async Task Invalid_file_writes_nothing_and_charging_never_changes_tax_ledgers()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new HomeChargingService(db, new ChargingCsvParser());
        var profile = await ActiveProfileAsync(service, company.Id);

        await Assert.ThrowsAsync<ChargingCsvValidationException>(() => service.ImportAsync(
            "owner-1", company.Id, profile.Id, Csv("wrong;1000;A"), Now));

        Assert.Empty(await db.ChargingImportBatches.ToListAsync());
        Assert.Empty(await db.ChargingSessions.ToListAsync());
        Assert.Empty(await db.CostBookings.ToListAsync());
        Assert.Empty(await db.KpirEntries.ToListAsync());
        Assert.Empty(await db.VatPurchaseEntries.ToListAsync());
    }

    [Fact]
    public async Task Non_owner_cannot_manage_or_read_charging_data()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new HomeChargingService(db, new ChargingCsvParser());
        var profile = await ActiveProfileAsync(service, company.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ImportAsync(
            "owner-2", company.Id, profile.Id, Csv("2026-09-01 01:00;1000;A"), Now));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveProfileAsync(
            "owner-2", company.Id, ProfileCommand(), Now));
        Assert.Null(await service.GetMonthAsync("owner-2", company.Id, Month));
    }

    private static async Task<ChargingProfileSnapshot> ActiveProfileAsync(HomeChargingService service, Guid companyId)
    {
        var profile = await service.SaveProfileAsync("owner-1", companyId, ProfileCommand(), Now);
        return await service.ActivateProfileAsync("owner-1", profile.Id, whConfirmed: true, Now);
    }

    private static SaveChargingProfileCommand ProfileCommand()
        => new(';', "started", "energy", "yyyy-MM-dd HH:mm", "Europe/Warsaw", "session-id");

    private static MemoryStream Csv(params string[] rows)
        => new(Encoding.UTF8.GetBytes($"started;energy;session-id\n{string.Join('\n', rows)}"));

    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"home-charging-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Company> SeedCompanyAsync(AppDbContext db)
    {
        var company = Company.Register(
            "owner-1", "Testowa Firma", "1234563218", "Testowy adres", new DateOnly(2026, 1, 1),
            "Testowy Klient", "1234563218", "Adres klienta", "Testowa usługa",
            1_500m, 23m, 0.91m, VehicleArrangement.None, Now);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }
}
