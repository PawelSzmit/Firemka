using Firemka.Domain.Vehicles;

namespace Firemka.Application.Vehicles;

public sealed record CreateVehiclePolicyCommand(
    Guid CompanyId,
    VehicleCostKind Kind,
    DateOnly EffectiveFromMonth);

public sealed record ActivateVehiclePolicyCommand(
    decimal VatDeductionPercent,
    decimal KpirCostPercent,
    string EvidenceReference);

public sealed record VehiclePolicySnapshot(
    Guid Id,
    Guid CompanyId,
    VehicleCostKind Kind,
    DateOnly EffectiveFromMonth,
    int VersionNumber,
    Guid? PreviousPolicyId,
    decimal? VatDeductionPercent,
    decimal? KpirCostPercent,
    string? EvidenceReference,
    VehiclePolicyStatus Status,
    IReadOnlyList<string> MissingRequirements);

public interface IVehiclePolicyService
{
    Task<IReadOnlyList<VehiclePolicySnapshot>> ListAsync(
        string ownerUserId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<VehiclePolicySnapshot> CreatePendingAsync(
        string ownerUserId,
        CreateVehiclePolicyCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<VehiclePolicySnapshot> ActivateAsync(
        string ownerUserId,
        Guid policyId,
        ActivateVehiclePolicyCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<VehiclePolicySnapshot> CreateRevisionAsync(
        string ownerUserId,
        Guid policyId,
        DateOnly effectiveFromMonth,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        string ownerUserId,
        Guid policyId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
