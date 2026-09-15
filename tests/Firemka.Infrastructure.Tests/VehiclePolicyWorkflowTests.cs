using Firemka.Application.Vehicles;
using Firemka.Domain.Companies;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Persistence;
using Firemka.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class VehiclePolicyWorkflowTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private static readonly DateOnly Month = new(2026, 9, 1);

    [Fact]
    public async Task Four_categories_remain_independent_and_none_is_active_by_default()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new VehiclePolicyService(db);

        foreach (var kind in Enum.GetValues<VehicleCostKind>())
        {
            await service.CreatePendingAsync("owner-1", new CreateVehiclePolicyCommand(company.Id, kind, Month), Now);
        }

        var policies = await service.ListAsync("owner-1", company.Id);
        Assert.Equal(4, policies.Count);
        Assert.All(policies, item => Assert.Equal(VehiclePolicyStatus.PendingEvidence, item.Status));
        Assert.All(policies, item => Assert.Contains(item.MissingRequirements, value => value.Contains("procent", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Retry_returns_the_existing_pending_policy()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new VehiclePolicyService(db);
        var command = new CreateVehiclePolicyCommand(company.Id, VehicleCostKind.Operation, Month);

        var first = await service.CreatePendingAsync("owner-1", command, Now);
        var second = await service.CreatePendingAsync("owner-1", command, Now.AddMinutes(1));

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await db.VehicleCostPolicies.ToListAsync());
    }

    [Fact]
    public async Task Revision_preserves_previous_period_and_starts_without_tax_defaults()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new VehiclePolicyService(db);
        var first = await service.CreatePendingAsync(
            "owner-1",
            new CreateVehiclePolicyCommand(company.Id, VehicleCostKind.Operation, Month),
            Now);
        await service.ActivateAsync("owner-1", first.Id, new ActivateVehiclePolicyCommand(50m, 75m, "notatka testowa"), Now);

        var revision = await service.CreateRevisionAsync("owner-1", first.Id, Month.AddMonths(1), Now.AddDays(1));

        Assert.Equal(2, revision.VersionNumber);
        Assert.Equal(first.Id, revision.PreviousPolicyId);
        Assert.Null(revision.VatDeductionPercent);
        Assert.Equal(2, await db.VehicleCostPolicies.CountAsync());
        Assert.Equal(VehiclePolicyStatus.Active, (await db.VehicleCostPolicies.SingleAsync(item => item.Id == first.Id)).Status);
    }

    [Fact]
    public async Task Non_owner_cannot_list_or_change_policies()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var service = new VehiclePolicyService(db);
        var policy = await service.CreatePendingAsync(
            "owner-1",
            new CreateVehiclePolicyCommand(company.Id, VehicleCostKind.Insurance, Month),
            Now);

        Assert.Empty(await service.ListAsync("owner-2", company.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ActivateAsync(
            "owner-2", policy.Id, new ActivateVehiclePolicyCommand(50m, 50m, "notatka"), Now));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreatePendingAsync(
            "owner-2", new CreateVehiclePolicyCommand(company.Id, VehicleCostKind.Operation, Month), Now));
    }

    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"vehicle-policies-{Guid.NewGuid():N}")
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
