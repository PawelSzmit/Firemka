using Firemka.Infrastructure.Persistence;

namespace Firemka.Infrastructure.Tests;

public sealed class MigrationContractTests
{
    [Fact]
    public void Initial_identity_migration_is_part_of_the_infrastructure_assembly()
    {
        var migration = typeof(AppDbContext).Assembly.GetType(
            "Firemka.Infrastructure.Persistence.Migrations.InitialIdentity");

        Assert.NotNull(migration);
    }

    [Fact]
    public void Company_profiles_migration_is_part_of_the_infrastructure_assembly()
    {
        var migration = typeof(AppDbContext).Assembly.GetType(
            "Firemka.Infrastructure.Persistence.Migrations.CompanyProfilesAndPeriods");

        Assert.NotNull(migration);
    }

    [Fact]
    public void Incoming_documents_migration_is_part_of_the_infrastructure_assembly()
    {
        var migration = typeof(AppDbContext).Assembly.GetType(
            "Firemka.Infrastructure.Persistence.Migrations.IncomingDocumentsKsefAndOcr");

        Assert.NotNull(migration);
    }

    [Fact]
    public void Phase_six_migration_is_part_of_the_infrastructure_assembly()
    {
        var migration = typeof(AppDbContext).Assembly.GetType(
            "Firemka.Infrastructure.Persistence.Migrations.Phase6CostAccountingVehiclesCharging");

        Assert.NotNull(migration);
    }
}
