# Phase 6A Cost Rules and Ledgers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn confirmed incoming expense documents into owner-approved, explainable and idempotent KPiR/VAT entries, with automation only for an exact approved fingerprint.

**Architecture:** `Firemka.Domain.Accounting` owns fingerprints, immutable rule revisions, booking decisions and ledger-entry invariants. `ICostAccountingService` orchestrates owned `SourceDocument` records and PostgreSQL persistence; Razor Pages call only this service. A booking stores the complete decision snapshot and points to the exact rule revision used.

**Tech Stack:** .NET 10, C# 14, EF Core 10, PostgreSQL 18, ASP.NET Core Razor Pages, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-11-phase-6-accounting-rules-charging-design.md`

## Global Constraints

- A first document or changed fingerprint never books automatically.
- Amount and ordinary issue-date changes do not participate in matching.
- Seller, country, currency, VAT profile/rate and service kind must match exactly.
- Tax percentages and period policies are explicit owner inputs; the application supplies no defaults.
- `Apply only to this document` and `Apply to future documents` are separate commands.
- KPiR and VAT are separate records sourced from one booking.
- No commit, push, deployment or real-company data in this execution.

---

### Task 1: Domain fingerprint and immutable rule revisions

**Files:**
- Create: `src/Firemka.Domain/Accounting/CostDocumentFingerprint.cs`
- Create: `src/Firemka.Domain/Accounting/CostRule.cs`
- Test: `tests/Firemka.Domain.Tests/CostRuleTests.cs`

**Interfaces:**
- Produces: `CostDocumentFingerprint.Create(...)`, `CostRule.CreateInitial(...)`, `CostRule.CreateRevision(...)`, `CostRule.Matches(...)`, `CostRule.ResolvePeriod(...)`.

- [x] **Step 1: Write failing matching and revision tests**

```csharp
[Fact]
public void Amount_and_date_do_not_change_a_match_but_each_tax_fingerprint_field_does()
{
    var rule = CreateRule(Fingerprint("PL123", "PL", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai));
    Assert.True(rule.Matches(Fingerprint("PL123", "PL", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai)));
    Assert.False(rule.Matches(Fingerprint("PL999", "PL", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai)));
    Assert.False(rule.Matches(Fingerprint("PL123", "US", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai)));
    Assert.False(rule.Matches(Fingerprint("PL123", "PL", "EUR", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai)));
    Assert.False(rule.Matches(Fingerprint("PL123", "PL", "PLN", VatProfile.ForeignService, null, CostServiceKind.Ai)));
    Assert.False(rule.Matches(Fingerprint("PL123", "PL", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Vps)));
}

[Fact]
public void Revision_keeps_history_and_manual_period_policy_disables_automatic_booking()
{
    var first = CreateRule(Fingerprint("PL123", "PL", "PLN", VatProfile.DomesticTaxed, 23m, CostServiceKind.Ai));
    var second = first.CreateRevision("Pozostałe wydatki", 50m, 75m, AccountingPeriodPolicy.Manual, AccountingPeriodPolicy.IssueMonth, "confirmed source", Now);
    Assert.Equal(2, second.VersionNumber);
    Assert.Equal(first.Id, second.PreviousRuleId);
    Assert.False(second.CanBookAutomatically);
}
```

- [x] **Step 2: Run the tests and verify the types are missing**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~CostRuleTests`

Expected: compile failure for `Firemka.Domain.Accounting` types.

- [x] **Step 3: Implement normalized value objects and enums**

```csharp
public enum VatProfile { DomesticTaxed, DomesticExempt, ForeignService, NoVat }
public enum CostServiceKind { Ai, Vps, Internet, VehicleLeaseOrRental, VehicleOperation, VehicleInsurance, PublicCharging, Other }
public enum AccountingPeriodPolicy { IssueMonth, NextMonth, Manual }

public sealed record CostDocumentFingerprint(
    string SellerKey,
    string SellerCountryCode,
    string Currency,
    VatProfile VatProfile,
    decimal? VatRate,
    CostServiceKind ServiceKind)
{
    public static CostDocumentFingerprint Create(string sellerKey, string country, string currency, VatProfile profile, decimal? rate, CostServiceKind kind);
}
```

`Create` trims and uppercases seller/country/currency, requires lengths `2` and `3`, rejects blank seller, requires `0..100` for a supplied rate, requires a rate for `DomesticTaxed`, and clears the rate for profiles where it is not meaningful.

- [x] **Step 4: Implement immutable rule revisions**

```csharp
public sealed class CostRule
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; }
    public string FingerprintKey { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousRuleId { get; private set; }
    public CostDocumentFingerprint Fingerprint { get; private set; }
    public string KpirCategory { get; private set; }
    public decimal VatDeductionPercent { get; private set; }
    public decimal KpirCostPercent { get; private set; }
    public AccountingPeriodPolicy KpirPeriodPolicy { get; private set; }
    public AccountingPeriodPolicy VatPeriodPolicy { get; private set; }
    public string DecisionSource { get; private set; }
    public bool IsActive { get; private set; }
    public bool CanBookAutomatically => IsActive && KpirPeriodPolicy != AccountingPeriodPolicy.Manual && VatPeriodPolicy != AccountingPeriodPolicy.Manual;

    public static CostRule CreateInitial(Guid companyId, string ownerUserId, CostDocumentFingerprint fingerprint, string category, decimal vatPercent, decimal kpirPercent, AccountingPeriodPolicy kpirPeriod, AccountingPeriodPolicy vatPeriod, string source, DateTimeOffset nowUtc);
    public CostRule CreateRevision(string category, decimal vatPercent, decimal kpirPercent, AccountingPeriodPolicy kpirPeriod, AccountingPeriodPolicy vatPeriod, string source, DateTimeOffset nowUtc);
    public bool Matches(CostDocumentFingerprint fingerprint);
    public DateOnly ResolveKpirPeriod(DateOnly issueDate);
    public DateOnly ResolveVatPeriod(DateOnly issueDate);
    public void Deactivate(DateTimeOffset nowUtc);
}
```

Create `FingerprintKey` as uppercase SHA-256 of the normalized fingerprint JSON. Revision copies company, owner and fingerprint, accepts the newly confirmed KPiR category, increments the version and points to `PreviousRuleId`; the caller deactivates the previous row in the same transaction.

- [x] **Step 5: Run domain tests**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~CostRuleTests`

Expected: PASS.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`

Expected: only intended uncommitted workspace files; do not commit.

---

### Task 2: Booking decision and separate KPiR/VAT entries

**Files:**
- Create: `src/Firemka.Domain/Accounting/CostBooking.cs`
- Create: `src/Firemka.Domain/Accounting/KpirEntry.cs`
- Create: `src/Firemka.Domain/Accounting/VatPurchaseEntry.cs`
- Test: `tests/Firemka.Domain.Tests/CostBookingTests.cs`

**Interfaces:**
- Consumes: `CostDocumentFingerprint`, `CostRule`.
- Produces: `CostBooking.CreatePending(...)`, `CostBooking.Book(...)`, `CostBookingCalculation`, `KpirEntry.Create(...)`, `VatPurchaseEntry.Create(...)`.

- [x] **Step 1: Write failing calculation and state tests**

```csharp
[Fact]
public void Booking_calculates_vat_then_kpir_and_rounds_each_final_result_to_grosze()
{
    var booking = CostBooking.CreatePending(DocumentId, CompanyId, "owner", Fingerprint(), 123m, 23m, IssueDate, Now);
    var calculation = booking.Book(ruleId: null, "Pozostałe wydatki", 50m, 75m, KpirMonth, VatMonth, "owner decision", automatic: false, Now);
    Assert.Equal(11.50m, calculation.DeductibleVatAmount);
    Assert.Equal(83.63m, calculation.KpirAmount);
    Assert.Equal(CostBookingStatus.BookedManually, booking.Status);
}

[Fact]
public void Invalid_amounts_or_second_booking_are_rejected()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => CostBooking.CreatePending(DocumentId, CompanyId, "owner", Fingerprint(), 100m, 101m, IssueDate, Now));
    var booking = CostBooking.CreatePending(DocumentId, CompanyId, "owner", Fingerprint(), 100m, 23m, IssueDate, Now);
    booking.Book(null, "Koszt", 0m, 0m, KpirMonth, VatMonth, "excluded", false, Now);
    Assert.Throws<InvalidOperationException>(() => booking.Book(null, "Koszt", 0m, 0m, KpirMonth, VatMonth, "again", false, Now));
}
```

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~CostBookingTests`

Expected: compile failure for missing booking types.

- [x] **Step 3: Implement booking and calculation**

```csharp
public enum CostBookingStatus { PendingReview, BookedManually, BookedAutomatically }
public sealed record CostBookingCalculation(decimal DeductibleVatAmount, decimal KpirAmount);

var deductibleVat = decimal.Round(InputVatAmount * vatDeductionPercent / 100m, 2, MidpointRounding.AwayFromZero);
var kpirAmount = decimal.Round((GrossAmount - deductibleVat) * kpirCostPercent / 100m, 2, MidpointRounding.AwayFromZero);
```

Persist the fingerprint fields as scalar booking properties, require `0 <= input VAT <= gross`, require both percentages in `0..100`, store category, periods, explanation, `RuleId`, `Automatic`, calculated amounts and a rotated concurrency stamp.

- [x] **Step 4: Implement separate ledger records**

```csharp
public static KpirEntry Create(Guid bookingId, Guid sourceDocumentId, string ownerUserId, DateOnly period, string category, decimal amount, Guid? ruleId, DateTimeOffset nowUtc);
public static VatPurchaseEntry Create(Guid bookingId, Guid sourceDocumentId, string ownerUserId, DateOnly period, decimal inputVat, decimal deductibleVat, Guid? ruleId, DateTimeOffset nowUtc);
```

Both types require non-empty IDs, owner, first-day-of-month periods and non-negative amounts. A zero amount is a deliberate `Included = false` entry.

- [x] **Step 5: Run domain tests**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter 'FullyQualifiedName~CostBookingTests|FullyQualifiedName~CostRuleTests'`

Expected: PASS.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 3: Application contracts, orchestration and persistence

**Files:**
- Create: `src/Firemka.Application/Accounting/CostAccountingContracts.cs`
- Create: `src/Firemka.Infrastructure/Accounting/CostAccountingService.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Firemka.Infrastructure/DependencyInjection.cs`
- Create: `tests/Firemka.Infrastructure.Tests/CostAccountingWorkflowTests.cs`

**Interfaces:**
- Produces: `ICostAccountingService.PrepareAsync`, `ConfirmForDocumentAsync`, `ConfirmAndApplyFutureAsync`, `GetReviewAsync`, `ListKpirAsync`, `ListVatAsync`.

- [x] **Step 1: Write failing first-rule and automatic-match workflow tests**

```csharp
[Fact]
public async Task First_document_waits_then_future_exact_match_books_once()
{
    var first = await service.PrepareAsync("owner", firstDocumentId, Input(gross: 123m, vat: 23m), Now);
    Assert.Equal(CostBookingStatus.PendingReview, first.Status);
    Assert.Empty(await db.KpirEntries.ToListAsync());

    await service.ConfirmAndApplyFutureAsync("owner", first.Id, Decision(), Now);
    var second = await service.PrepareAsync("owner", secondDocumentId, Input(gross: 246m, vat: 46m), Now.AddDays(2));
    var replay = await service.PrepareAsync("owner", secondDocumentId, Input(gross: 246m, vat: 46m), Now.AddDays(2));

    Assert.Equal(CostBookingStatus.BookedAutomatically, second.Status);
    Assert.Equal(second.Id, replay.Id);
    Assert.Equal(2, await db.KpirEntries.CountAsync());
    Assert.Equal(2, await db.VatPurchaseEntries.CountAsync());
}
```

Add tests proving: changed country/currency/VAT/service produces pending review with named differences; `ConfirmForDocumentAsync` creates no rule; a non-owner cannot prepare, read or confirm; a manual period rule never auto-books; duplicate concurrent prepare yields one booking and one ledger pair.

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~CostAccountingWorkflowTests`

Expected: compile failure for missing service and DbSets.

- [x] **Step 3: Define exact contracts**

```csharp
public sealed record PrepareCostCommand(string SellerCountryCode, VatProfile VatProfile, decimal? VatRate, CostServiceKind ServiceKind, decimal GrossAmount, decimal InputVatAmount);
public sealed record CostDecisionCommand(string KpirCategory, decimal VatDeductionPercent, decimal KpirCostPercent, AccountingPeriodPolicy KpirPeriodPolicy, AccountingPeriodPolicy VatPeriodPolicy, DateOnly KpirPeriod, DateOnly VatPeriod, string DecisionSource);
public interface ICostAccountingService
{
    Task<CostBookingSnapshot> PrepareAsync(string ownerUserId, Guid sourceDocumentId, PrepareCostCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task ConfirmForDocumentAsync(string ownerUserId, Guid bookingId, CostDecisionCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task ConfirmAndApplyFutureAsync(string ownerUserId, Guid bookingId, CostDecisionCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<CostReviewSnapshot?> GetReviewAsync(string ownerUserId, Guid sourceDocumentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpirEntrySnapshot>> ListKpirAsync(string ownerUserId, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VatEntrySnapshot>> ListVatAsync(string ownerUserId, int year, CancellationToken cancellationToken = default);
}
```

- [x] **Step 4: Implement transactional service behavior**

`PrepareAsync` must load an owned `SourceDocument` in `RuleToDefine`, derive seller key from tax ID or explicitly normalized seller name, require the command currency/gross to equal confirmed source data, create one pending booking, locate one active exact rule, and auto-book only when `CanBookAutomatically`. `ConfirmForDocumentAsync` books without a rule. `ConfirmAndApplyFutureAsync` deactivates an active rule with the same fingerprint, creates its next revision (or initial version), then books with that exact ID. Both confirmation methods add KPiR/VAT entries and transition the source document to `Booked` in one transaction.

Handle unique violations by clearing the tracker and returning the existing owned booking. Never swallow a unique violation unless the existing booking or ledger pair proves an idempotent replay.

- [x] **Step 5: Configure EF mappings and indexes**

Add DbSets and mappings for owned fingerprint scalar fields. Required unique indexes:

```csharp
entity.HasIndex(x => x.SourceDocumentId).IsUnique();
entity.HasIndex(x => new { x.CompanyId, x.FingerprintKey, x.VersionNumber }).IsUnique();
entity.HasIndex(x => new { x.CompanyId, x.FingerprintKey }).IsUnique().HasFilter("\"IsActive\" = TRUE");
entity.HasIndex(x => x.BookingId).IsUnique(); // once for KPiR and once for VAT
```

Map money to `numeric(18,2)`, percentages to `numeric(5,2)`, enums to strings, JSON explanation/differences to `jsonb`, and concurrency stamps as concurrency tokens. Register the service scoped.

- [x] **Step 6: Run targeted tests**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~CostAccountingWorkflowTests`

Expected: PASS.

- [x] **Step 7: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 4: Migration, PostgreSQL concurrency and owner-facing pages

**Files:**
- Create: `src/Firemka.Infrastructure/Persistence/Migrations/<timestamp>_CostAccountingAndLedgers.cs` with EF-generated designer/snapshot update
- Modify: `tests/Firemka.Infrastructure.Tests/PostgresPersistenceTests.cs`
- Create: `src/Firemka.Web/Pages/Expenses/Review.cshtml`
- Create: `src/Firemka.Web/Pages/Expenses/Review.cshtml.cs`
- Modify: `src/Firemka.Web/Pages/Expenses/Index.cshtml`
- Modify: `src/Firemka.Web/Pages/Expenses/Index.cshtml.cs`
- Modify: `src/Firemka.Web/Pages/Books/Index.cshtml`
- Create: `src/Firemka.Web/Pages/Books/Index.cshtml.cs`
- Create: `tests/Firemka.Web.Tests/CostAccountingPagesTests.cs`

**Interfaces:**
- Consumes: `ICostAccountingService`.
- Produces: `/Expenses`, `/Expenses/Review/{id}`, `/Books?year=YYYY`.

- [x] **Step 1: Write failing web tests**

Test authenticated rendering, non-owner `404`, both separate POST handlers, anti-forgery validation, validation errors that preserve input, named fingerprint differences, and separate KPiR/VAT tables. Verify that choosing only-document leaves rule count zero and future-rule creates exactly one active revision.

```csharp
var review = await client.GetAsync($"/Expenses/Review/{documentId}");
Assert.Equal(HttpStatusCode.OK, review.StatusCode);
Assert.Contains("Zastosuj tylko do tego dokumentu", await review.Content.ReadAsStringAsync());
Assert.Contains("Zastosuj także do kolejnych", await review.Content.ReadAsStringAsync());
```

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Web.Tests/Firemka.Web.Tests.csproj --filter FullyQualifiedName~CostAccountingPagesTests`

Expected: FAIL because routes and handlers do not exist.

- [x] **Step 3: Implement pages with explicit copy**

`/Expenses` lists `RuleToDefine` and pending bookings. Review shows confirmed source fields, five fingerprint fields, gross/input VAT, calculated preview and exact differences. Use two forms and handlers `OnPostConfirmDocumentAsync` and `OnPostConfirmFutureAsync`; do not infer the future choice from a checkbox. `/Books` uses two tables and shows source document link, period, category, amount, automatic/manual, rule version and the text `Nie ujęto` for zero entries.

- [x] **Step 4: Generate the migration**

Run: `dotnet ef migrations add CostAccountingAndLedgers --project src/Firemka.Infrastructure --startup-project src/Firemka.Web`

Inspect generated SQL model: no destructive drop, required unique indexes present, all new columns non-null or safely defaulted because tables are new.

- [x] **Step 5: Extend PostgreSQL proof**

Create isolated data, prepare the same document concurrently, confirm once, replay confirmation, and assert one booking, one KPiR entry, one VAT entry and one active rule. Migrate down to `20260911135310_SalesInvoicesAndOutgoingKsef`, assert new tables absent, then migrate up and rerun the workflow.

- [x] **Step 6: Run web, infrastructure and PostgreSQL tests**

Run the two targeted projects, then the isolated PostgreSQL test with `FIREMKA_TEST_POSTGRES`.

Expected: PASS with one conditional skip only when PostgreSQL is intentionally not configured.

- [x] **Step 7: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.
