using Firemka.Domain.Vehicles;

namespace Firemka.Domain.Tests;

public sealed class VehicleCostPolicyTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Month = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public void Pending_policy_cannot_activate_without_evidence_and_explicit_percentages()
    {
        var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner-1", VehicleCostKind.Operation, Month, Now);

        Assert.Throws<InvalidOperationException>(() => policy.Activate(null, 50m, "faktura", Now));
        Assert.Throws<InvalidOperationException>(() => policy.Activate(50m, null, "faktura", Now));
        Assert.Throws<ArgumentException>(() => policy.Activate(50m, 75m, " ", Now));

        policy.Activate(50m, 75m, "potwierdzona notatka do umowy", Now);

        Assert.Equal(VehiclePolicyStatus.Active, policy.Status);
        Assert.Equal(50m, policy.VatDeductionPercent);
        Assert.Equal(75m, policy.KpirCostPercent);
    }

    [Theory]
    [InlineData(VehicleCostKind.LeaseOrRental)]
    [InlineData(VehicleCostKind.Operation)]
    [InlineData(VehicleCostKind.Insurance)]
    [InlineData(VehicleCostKind.PublicCharging)]
    public void Every_vehicle_cost_kind_has_its_own_pending_policy_without_defaults(VehicleCostKind kind)
    {
        var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner-1", kind, Month, Now);

        Assert.Equal(kind, policy.Kind);
        Assert.Equal(VehiclePolicyStatus.PendingEvidence, policy.Status);
        Assert.Null(policy.VatDeductionPercent);
        Assert.Null(policy.KpirCostPercent);
        Assert.Null(policy.EvidenceReference);
    }

    [Fact]
    public void Revision_keeps_identity_and_history_but_does_not_copy_tax_decisions()
    {
        var first = VehicleCostPolicy.CreatePending(CompanyId, "owner-1", VehicleCostKind.Insurance, Month, Now);
        first.Activate(50m, 100m, "polisa testowa", Now);

        var second = first.CreateRevision(new DateOnly(2026, 10, 1), Now.AddDays(1));

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.Id, second.PreviousPolicyId);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(VehiclePolicyStatus.PendingEvidence, second.Status);
        Assert.Null(second.VatDeductionPercent);
        Assert.Null(second.KpirCostPercent);
        Assert.Equal(VehiclePolicyStatus.Active, first.Status);
    }

    [Fact]
    public void Invalid_month_percentage_or_reactivation_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => VehicleCostPolicy.CreatePending(
            CompanyId, "owner-1", VehicleCostKind.Operation, new DateOnly(2026, 9, 2), Now));

        var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner-1", VehicleCostKind.Operation, Month, Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Activate(101m, 50m, "notatka", Now));

        policy.Activate(50m, 50m, "notatka", Now);
        Assert.Throws<InvalidOperationException>(() => policy.Activate(50m, 50m, "druga notatka", Now));
    }

    [Fact]
    public void Deactivation_preserves_the_decision_evidence()
    {
        var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner-1", VehicleCostKind.PublicCharging, Month, Now);
        policy.Activate(0m, 0m, "brak podstawy podatkowej", Now);

        policy.Deactivate(Now.AddDays(1));

        Assert.Equal(VehiclePolicyStatus.Inactive, policy.Status);
        Assert.Equal("brak podstawy podatkowej", policy.EvidenceReference);
        Assert.Equal(0m, policy.VatDeductionPercent);
        Assert.Equal(0m, policy.KpirCostPercent);
    }
}
