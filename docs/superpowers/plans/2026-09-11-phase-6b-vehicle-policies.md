# Phase 6B Vehicle Cost Policies Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide four independent, versioned vehicle-cost policies whose activation is impossible until the owner supplies explicit tax parameters and evidence.

**Architecture:** `Firemka.Domain.Vehicles` owns the policy categories, lifecycle and immutable revision chain. `IVehiclePolicyService` enforces owner/company isolation and performs revision changes transactionally. Razor Pages display each category separately and never prefill a tax percentage.

**Tech Stack:** .NET 10, C# 14, EF Core 10, PostgreSQL 18, ASP.NET Core Razor Pages, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-11-phase-6-accounting-rules-charging-design.md`

## Global Constraints

- Lease/rental, operation, insurance and public charging remain independent.
- No active policy is seeded and no percentage is inferred or prefilled.
- Activation requires a first-of-month effective date, both explicit percentages and a non-empty evidence reference.
- Revisions are immutable; an earlier effective period stays available for historical dates and is not overwritten by a future revision.
- No commit, push, deployment or real-company data in this execution.

---

### Task 1: Vehicle policy domain and activation guardrails

**Files:**
- Create: `src/Firemka.Domain/Vehicles/VehicleCostPolicy.cs`
- Test: `tests/Firemka.Domain.Tests/VehicleCostPolicyTests.cs`

**Interfaces:**
- Produces: `VehicleCostPolicy.CreatePending(...)`, `VehicleCostPolicy.Activate(...)`, `VehicleCostPolicy.CreateRevision(...)`, `VehicleCostPolicy.Deactivate(...)`.

- [x] **Step 1: Write failing lifecycle and category-isolation tests**

```csharp
[Fact]
public void Pending_policy_cannot_activate_without_evidence_and_explicit_percentages()
{
    var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner", VehicleCostKind.Operation, Month, Now);
    Assert.Throws<InvalidOperationException>(() => policy.Activate(null, 50m, "invoice", Now));
    Assert.Throws<ArgumentException>(() => policy.Activate(50m, 75m, " ", Now));

    policy.Activate(50m, 75m, "confirmed contract note", Now);
    Assert.Equal(VehiclePolicyStatus.Active, policy.Status);
}

[Theory]
[InlineData(VehicleCostKind.LeaseOrRental)]
[InlineData(VehicleCostKind.Operation)]
[InlineData(VehicleCostKind.Insurance)]
[InlineData(VehicleCostKind.PublicCharging)]
public void Every_vehicle_cost_kind_has_its_own_policy(VehicleCostKind kind)
{
    var policy = VehicleCostPolicy.CreatePending(CompanyId, "owner", kind, Month, Now);
    Assert.Equal(kind, policy.Kind);
    Assert.Null(policy.VatDeductionPercent);
    Assert.Null(policy.KpirCostPercent);
}
```

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~VehicleCostPolicyTests`

Expected: compile failure for missing vehicle policy types.

- [x] **Step 3: Implement the exact domain model**

```csharp
public enum VehicleCostKind { LeaseOrRental, Operation, Insurance, PublicCharging }
public enum VehiclePolicyStatus { PendingEvidence, Active, Inactive }

public sealed class VehicleCostPolicy
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; }
    public VehicleCostKind Kind { get; private set; }
    public DateOnly EffectiveFromMonth { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousPolicyId { get; private set; }
    public decimal? VatDeductionPercent { get; private set; }
    public decimal? KpirCostPercent { get; private set; }
    public string? EvidenceReference { get; private set; }
    public VehiclePolicyStatus Status { get; private set; }

    public static VehicleCostPolicy CreatePending(Guid companyId, string ownerUserId, VehicleCostKind kind, DateOnly effectiveFromMonth, DateTimeOffset nowUtc);
    public void Activate(decimal? vatPercent, decimal? kpirPercent, string evidenceReference, DateTimeOffset nowUtc);
    public VehicleCostPolicy CreateRevision(DateOnly effectiveFromMonth, DateTimeOffset nowUtc);
    public void Deactivate(DateTimeOffset nowUtc);
}
```

Require non-empty company/owner, a first-day-of-month effective date and percentages in `0..100`. `CreateRevision` returns a pending version with incremented number and `PreviousPolicyId`; it does not inherit percentages or evidence implicitly.

- [x] **Step 4: Add revision and validation tests**

Prove that an invalid month or percentage is rejected, activation is one-way, a revision preserves category/company/owner but starts pending, and deactivation preserves all decision evidence.

- [x] **Step 5: Run domain tests**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~VehicleCostPolicyTests`

Expected: PASS.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 2: Application service, persistence and owner isolation

**Files:**
- Create: `src/Firemka.Application/Vehicles/VehiclePolicyContracts.cs`
- Create: `src/Firemka.Infrastructure/Vehicles/VehiclePolicyService.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Firemka.Infrastructure/DependencyInjection.cs`
- Create: `tests/Firemka.Infrastructure.Tests/VehiclePolicyWorkflowTests.cs`

**Interfaces:**
- Produces: `IVehiclePolicyService.ListAsync`, `CreatePendingAsync`, `ActivateAsync`, `CreateRevisionAsync`, `DeactivateAsync`.

- [x] **Step 1: Write failing persistence workflow tests**

```csharp
[Fact]
public async Task Four_categories_remain_independent_and_none_is_active_by_default()
{
    foreach (var kind in Enum.GetValues<VehicleCostKind>())
        await service.CreatePendingAsync("owner", new CreateVehiclePolicyCommand(CompanyId, kind, Month), Now);

    var policies = await service.ListAsync("owner", CompanyId);
    Assert.Equal(4, policies.Count);
    Assert.All(policies, item => Assert.Equal(VehiclePolicyStatus.PendingEvidence, item.Status));
}

[Fact]
public async Task Revision_preserves_previous_effective_period_without_erasing_it()
{
    var first = await CreateAndActivateAsync(VehicleCostKind.Operation);
    var revision = await service.CreateRevisionAsync("owner", first.Id, NextMonth, Now);
    Assert.Equal(2, revision.VersionNumber);
    Assert.Equal(first.Id, revision.PreviousPolicyId);
    Assert.Equal(2, await db.VehicleCostPolicies.CountAsync());
}
```

Add tests proving a non-owner cannot list/change policies, duplicate create/retry is idempotent, and concurrent activation or revision retries cannot create divergent copies for the same company/category/effective month.

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~VehiclePolicyWorkflowTests`

Expected: compile failure for missing contracts, service and `DbSet`.

- [x] **Step 3: Define exact contracts**

```csharp
public sealed record CreateVehiclePolicyCommand(Guid CompanyId, VehicleCostKind Kind, DateOnly EffectiveFromMonth);
public sealed record ActivateVehiclePolicyCommand(decimal VatDeductionPercent, decimal KpirCostPercent, string EvidenceReference);

public interface IVehiclePolicyService
{
    Task<IReadOnlyList<VehiclePolicySnapshot>> ListAsync(string ownerUserId, Guid companyId, CancellationToken cancellationToken = default);
    Task<VehiclePolicySnapshot> CreatePendingAsync(string ownerUserId, CreateVehiclePolicyCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<VehiclePolicySnapshot> ActivateAsync(string ownerUserId, Guid policyId, ActivateVehiclePolicyCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<VehiclePolicySnapshot> CreateRevisionAsync(string ownerUserId, Guid policyId, DateOnly effectiveFromMonth, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task DeactivateAsync(string ownerUserId, Guid policyId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
```

Snapshots expose the policy fields plus a plain-language `MissingRequirements` list. `CreatePendingAsync` verifies that the company belongs to the owner before writing.

- [x] **Step 4: Map persistence and transaction boundaries**

Add `DbSet<VehicleCostPolicy>`. Configure decimals as `numeric(5,2)`, owner/company indexes, a unique revision number within the policy chain, and a PostgreSQL partial unique index that permits at most one `Active` row for `(CompanyId, Kind, EffectiveFromMonth)`. Translate uniqueness races into the existing result or a clear domain conflict; never silently overwrite.

- [x] **Step 5: Implement service and run workflow tests**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~VehiclePolicyWorkflowTests`

Expected: PASS with InMemory/SQLite-equivalent workflow coverage; PostgreSQL uniqueness is proved in Task 3.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 3: Vehicle policy page, migration and PostgreSQL proof

**Files:**
- Create: `src/Firemka.Web/Pages/Expenses/VehiclePolicies.cshtml`
- Create: `src/Firemka.Web/Pages/Expenses/VehiclePolicies.cshtml.cs`
- Modify: `src/Firemka.Web/Pages/Expenses/Index.cshtml`
- Modify: `src/Firemka.Web/Pages/Shared/_Layout.cshtml`
- Create: `tests/Firemka.Web.Tests/VehiclePoliciesPageTests.cs`
- Create: `tests/Firemka.Infrastructure.Tests/PostgresVehiclePolicyTests.cs`
- Create: `src/Firemka.Infrastructure/Persistence/Migrations/*_VehicleCostPolicies.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`

- [x] **Step 1: Write failing page tests**

Test authenticated owner rendering, anonymous redirect, four separately labelled cards, no prefilled percentage values, visible missing-evidence block, activation validation and owner isolation.

- [x] **Step 2: Implement the Razor Page**

Render Polish labels `Najem lub leasing`, `Eksploatacja`, `Ubezpieczenie`, `Publiczne ładowanie`. Pending cards state exactly what is missing. Activation accepts both percentages and evidence; revision creation is a separate action. Use normal form validation summaries and antiforgery protection. Link the page from expenses/navigation without activating or seeding any policy.

- [x] **Step 3: Run web tests**

Run: `dotnet test tests/Firemka.Web.Tests/Firemka.Web.Tests.csproj --filter FullyQualifiedName~VehiclePoliciesPageTests`

Expected: PASS.

- [x] **Step 4: Generate and inspect migration**

Run: `dotnet ef migrations add VehicleCostPolicies --project src/Firemka.Infrastructure --startup-project src/Firemka.Web`

Inspect the migration and snapshot. Confirm decimal precision, foreign keys/indexes and the PostgreSQL partial unique index; do not accept unrelated schema changes.

- [x] **Step 5: Prove PostgreSQL behavior**

Run the dedicated test with `FIREMKA_TEST_POSTGRES` against the isolated local test database. Prove migration up/down/up, owner isolation and concurrent active-policy uniqueness.

Expected: PASS without changing the user-facing Compose database.

- [x] **Step 6: Check responsive presentation**

Use synthetic policies only. Verify 390 px and 1440 px: no horizontal page overflow, labels remain connected to inputs, errors are readable, and all four category states are distinguishable.

- [x] **Step 7: Run scoped regression and record a no-commit checkpoint**

Run the domain, infrastructure and web vehicle-policy filters, then `git status --short`; do not commit.
