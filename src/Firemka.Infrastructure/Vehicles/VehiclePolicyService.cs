using Firemka.Application.Vehicles;
using Firemka.Domain.Vehicles;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Vehicles;

public sealed class VehiclePolicyService(AppDbContext dbContext) : IVehiclePolicyService
{
    public async Task<IReadOnlyList<VehiclePolicySnapshot>> ListAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default)
        => (await dbContext.VehicleCostPolicies.AsNoTracking()
                .Where(item => item.OwnerUserId == ownerUserId && item.CompanyId == companyId)
                .OrderBy(item => item.Kind)
                .ThenByDescending(item => item.EffectiveFromMonth)
                .ThenByDescending(item => item.VersionNumber)
                .ToListAsync(cancellationToken))
            .Select(ToSnapshot)
            .ToArray();

    public async Task<VehiclePolicySnapshot> CreatePendingAsync(
        string ownerUserId,
        CreateVehiclePolicyCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(command);
        var ownsCompany = await dbContext.Companies.AsNoTracking().AnyAsync(
            item => item.Id == command.CompanyId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (!ownsCompany)
        {
            throw new KeyNotFoundException("Nie znaleziono firmy.");
        }

        var existing = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
            item => item.CompanyId == command.CompanyId
                && item.OwnerUserId == ownerUserId
                && item.Kind == command.Kind
                && item.EffectiveFromMonth == command.EffectiveFromMonth,
            cancellationToken);
        if (existing is not null)
        {
            return ToSnapshot(existing);
        }

        var policy = VehicleCostPolicy.CreatePending(
            command.CompanyId,
            ownerUserId,
            command.Kind,
            command.EffectiveFromMonth,
            nowUtc);
        dbContext.VehicleCostPolicies.Add(policy);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(policy);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
                item => item.CompanyId == command.CompanyId
                    && item.OwnerUserId == ownerUserId
                    && item.Kind == command.Kind
                    && item.EffectiveFromMonth == command.EffectiveFromMonth,
                cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return ToSnapshot(replay);
        }
    }

    public async Task<VehiclePolicySnapshot> ActivateAsync(
        string ownerUserId,
        Guid policyId,
        ActivateVehiclePolicyCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var policy = await OwnedPolicyAsync(ownerUserId, policyId, cancellationToken);
        if (policy.Status == VehiclePolicyStatus.Active)
        {
            return ToSnapshot(policy);
        }

        policy.Activate(
            command.VatDeductionPercent,
            command.KpirCostPercent,
            command.EvidenceReference,
            nowUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(policy);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == policyId && item.OwnerUserId == ownerUserId,
                cancellationToken);
            if (replay?.Status == VehiclePolicyStatus.Active
                && replay.VatDeductionPercent == command.VatDeductionPercent
                && replay.KpirCostPercent == command.KpirCostPercent
                && replay.EvidenceReference == command.EvidenceReference.Trim())
            {
                return ToSnapshot(replay);
            }

            throw;
        }
    }

    public async Task<VehiclePolicySnapshot> CreateRevisionAsync(
        string ownerUserId,
        Guid policyId,
        DateOnly effectiveFromMonth,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
            item => item.PreviousPolicyId == policyId
                && item.OwnerUserId == ownerUserId
                && item.EffectiveFromMonth == effectiveFromMonth,
            cancellationToken);
        if (existing is not null)
        {
            return ToSnapshot(existing);
        }

        var policy = await OwnedPolicyAsync(ownerUserId, policyId, cancellationToken);
        var revision = policy.CreateRevision(effectiveFromMonth, nowUtc);
        dbContext.VehicleCostPolicies.Add(revision);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(revision);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
                item => item.PreviousPolicyId == policyId
                    && item.OwnerUserId == ownerUserId
                    && item.EffectiveFromMonth == effectiveFromMonth,
                cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return ToSnapshot(replay);
        }
    }

    public async Task DeactivateAsync(
        string ownerUserId,
        Guid policyId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var policy = await OwnedPolicyAsync(ownerUserId, policyId, cancellationToken);
        if (policy.Status == VehiclePolicyStatus.Inactive)
        {
            return;
        }

        policy.Deactivate(nowUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.VehicleCostPolicies.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == policyId && item.OwnerUserId == ownerUserId,
                cancellationToken);
            if (replay?.Status != VehiclePolicyStatus.Inactive)
            {
                throw;
            }
        }
    }

    private async Task<VehicleCostPolicy> OwnedPolicyAsync(
        string ownerUserId,
        Guid policyId,
        CancellationToken cancellationToken)
        => await dbContext.VehicleCostPolicies.SingleOrDefaultAsync(
            item => item.Id == policyId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono polityki samochodu.");

    private static VehiclePolicySnapshot ToSnapshot(VehicleCostPolicy policy)
    {
        var missing = new List<string>();
        if (policy.VatDeductionPercent is null || policy.KpirCostPercent is null)
        {
            missing.Add("Podaj jawnie procent VAT i procent kosztu KPiR.");
        }

        if (string.IsNullOrWhiteSpace(policy.EvidenceReference))
        {
            missing.Add("Dodaj referencję do umowy, dokumentu albo potwierdzonej notatki.");
        }

        return new VehiclePolicySnapshot(
            policy.Id,
            policy.CompanyId,
            policy.Kind,
            policy.EffectiveFromMonth,
            policy.VersionNumber,
            policy.PreviousPolicyId,
            policy.VatDeductionPercent,
            policy.KpirCostPercent,
            policy.EvidenceReference,
            policy.Status,
            missing);
    }
}
