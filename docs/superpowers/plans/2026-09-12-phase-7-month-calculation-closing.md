# Phase 7 Month Calculation and Closing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic, explainable PIT/VAT/health previews and immutable month-closing/correction versions, while blocking a real close until the rule set and month inputs are independently confirmed.

**Architecture:** `Firemka.Domain.Calculations` owns immutable rules, declarations, adjustments, calculations and settlement versions. `IMonthClosingService` builds an owner-scoped source snapshot from sales, KPiR, VAT and explicit adjustments, then persists a calculation and close atomically. Razor Pages show technical previews, blockers, confirmation evidence and history without generating or sending government files.

**Tech Stack:** .NET 10, C# 14, EF Core 10, PostgreSQL 18, ASP.NET Core Razor Pages, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-12-phase-7-month-calculation-closing-design.md`

## Global Constraints

- The 2026 built-in values are `ReferenceOnly`; code must never mark them independently confirmed.
- A technical preview is visibly labelled and cannot close a month.
- Only `TaxScale`, monthly active VAT and the configured ZUS profile are in Phase 7; do not add a ryczałt switch.
- Health income is a separate confirmed input bridge and never aliases PIT income silently.
- Foreign-service VAT and a late sales-invoice recognition period require explicit evidence instead of an inferred result.
- Closed calculations and settlements are immutable; a correction creates a successor version with a reason.
- Phase 7 does not generate/send JPK or ZUS files, record payments, close a year or prepare a full annual PIT.
- Use synthetic data only in tests and browser acceptance. Do not copy real owner documents into the repository.
- Do not commit, push or deploy during this execution.

---

### Task 1: Versioned calculation rules and deterministic calculation domain

**Files:**
- Create: `src/Firemka.Domain/Calculations/CalculationRuleSet.cs`
- Create: `src/Firemka.Domain/Calculations/MonthCalculation.cs`
- Create: `src/Firemka.Infrastructure/Calculations/CalculationReferenceCatalog.cs`
- Test: `tests/Firemka.Domain.Tests/MonthCalculationTests.cs`
- Test: `tests/Firemka.Infrastructure.Tests/CalculationReferenceCatalogTests.cs`

**Interfaces:**
- Produces: `CalculationRuleSet.CreateReference(...)`, `CalculationRuleSet.Confirm(...)`, `CalculationRuleSet.GetMinimumHealthBase(...)`, `MonthCalculation.Calculate(...)`, `CalculationReferenceCatalog.Create2026(...)`.
- Consumes: only validated scalar inputs and source lines; no database or web dependencies in the domain.

- [x] **Step 1: Write failing rule and calculation tests**

Cover these exact cases:

```csharp
[Fact]
public void Reference_rule_needs_a_new_evidenced_revision_before_closing()
{
    var reference = Rules2026();
    Assert.Equal(CalculationRuleTrust.ReferenceOnly, reference.Trust);
    var confirmed = reference.Confirm("opinia księgowej TEST/2026", new DateOnly(2026, 9, 12), Now);
    Assert.Equal(2, confirmed.VersionNumber);
    Assert.Equal(reference.Id, confirmed.PreviousRuleSetId);
    Assert.Equal(CalculationRuleTrust.IndependentlyConfirmed, confirmed.Trust);
}

[Theory]
[InlineData("100000", "8400")]
[InlineData("130000", "14000")]
public void Tax_scale_uses_both_brackets_and_whole_zloty_rounding(string income, string expectedTax)
{
    var result = Calculate(cumulativeIncome: decimal.Parse(income, CultureInfo.InvariantCulture));
    Assert.Equal(decimal.Parse(expectedTax, CultureInfo.InvariantCulture), result.CumulativePitTax);
}

[Fact]
public void Vat_surplus_carries_and_health_uses_previous_month_with_minimum()
{
    var result = Calculate(outputVat: 100m, inputVat: 300m, priorVatCarry: 50m, previousHealthIncome: 1000m);
    Assert.Equal(250m, result.VatCarryForward);
    Assert.Equal(0m, result.VatPayable);
    Assert.Equal(4806m, result.HealthBasis);
    Assert.Equal(432.54m, result.HealthContribution);
}
```

Also test: loss yields zero PIT; previous advances reduce only the current advance; a 1,000 PLN-or-less advance is still returned with `CanDeferPitPayment = true`; January/February minimum health bases differ; invalid percentages/thresholds/source/evidence are rejected; old rule remains unchanged.

- [x] **Step 2: Run tests and verify missing types**

Run:

```bash
dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~MonthCalculationTests
```

Expected: compile failure for `Firemka.Domain.Calculations`.

- [x] **Step 3: Implement `CalculationRuleSet`**

Use these public types and signatures:

```csharp
public enum CalculationRuleTrust { ReferenceOnly = 1, IndependentlyConfirmed = 2 }

public sealed record CalculationRuleValues(
    decimal PitThreshold,
    decimal PitLowerRatePercent,
    decimal PitHigherRatePercent,
    decimal PitReducingAmount,
    decimal PitPaymentOptionThreshold,
    decimal HealthRatePercent,
    int HealthMinimumChangeMonth,
    decimal HealthMinimumBeforeChange,
    decimal HealthMinimumFromChange);

public sealed class CalculationRuleSet
{
    public static CalculationRuleSet CreateReference(
        Guid companyId, string ownerUserId, int taxYear, CalculationRuleValues values,
        string officialSources, DateOnly capturedOn, DateTimeOffset nowUtc);

    public CalculationRuleSet Confirm(
        string independentEvidenceReference, DateOnly confirmedOn, DateTimeOffset nowUtc);

    public decimal GetMinimumHealthBase(DateOnly contributionMonth);
}
```

Normalize owner/source/evidence, validate `2000..2200`, positive threshold and health minimums, rates `0..100`, reducing/payment values `>= 0`, and change month `1..12`. `Confirm` creates a new ID, increments version, points to the predecessor, copies exact values/sources and stores evidence/date. It must not mutate the reference row.

- [x] **Step 4: Implement `MonthCalculation` and explanation lines**

Use:

```csharp
public enum CalculationTrust { TechnicalPreview = 1, ClosingEligible = 2 }
public sealed record CalculationSourceLine(string Code, string Label, decimal Amount, string SourceReference);
public sealed record MonthCalculationInput(
    Guid CompanyId, string OwnerUserId, DateOnly Month, Guid RuleSetId, Guid DeclarationId,
    Guid? PreviousCalculationId, string InputFingerprint,
    decimal RevenueMonth, decimal CostsMonth, decimal SocialContributionsMonth,
    decimal PitBaseAdjustmentMonth, decimal HealthIncomeAdjustmentMonth,
    decimal RevenueYtdBefore, decimal CostsYtdBefore, decimal SocialContributionsYtdBefore,
    decimal PitAdjustmentsYtdBefore, decimal PriorPitAdvancesDue,
    decimal PriorVatCarryForward, decimal PreviousMonthHealthIncome,
    decimal OutputVatMonth, decimal InputVatMonth,
    CalculationTrust Trust, IReadOnlyCollection<CalculationSourceLine> SourceLines);

public static MonthCalculation Calculate(
    MonthCalculationInput input, CalculationRuleSet rules, int versionNumber,
    Guid? previousVersionId, DateTimeOffset nowUtc);
```

Calculate cumulative revenue/cost/social/adjustment, income or loss, a non-negative whole-zloty PIT base, scale tax, current advance, VAT payable/carry, current separate health income, minimum health basis and cents-rounded contribution. Add the source lines plus formula lines in stable sequence. Reject mismatched owner/company/year/rule IDs, non-first-day months, negative non-adjustment totals, blank/invalid 64-character fingerprint and empty source references.

- [x] **Step 5: Implement the official-source 2026 reference catalog**

`CalculationReferenceCatalog.Create2026(companyId, owner, nowUtc)` returns a `ReferenceOnly` rule with exactly:

```text
PIT threshold: 120000.00
lower rate: 12.00
higher rate: 32.00
reducing amount: 3600.00
payment option threshold: 1000.00
health rate: 9.00
minimum change month: 2
minimum before change: 3499.50
minimum from change: 4806.00
captured on: 2026-09-12
```

Store only official HTTPS source URLs from the design. For another year throw a clear `InvalidOperationException` instead of copying 2026.

- [x] **Step 6: Run targeted tests**

Run both new test classes. Expected: PASS.

- [x] **Step 7: Record the no-commit checkpoint**

Run `git status --short`; do not commit.

---

### Task 2: Monthly declarations, adjustments and immutable settlement lifecycle

**Files:**
- Create: `src/Firemka.Domain/Calculations/MonthDeclaration.cs`
- Create: `src/Firemka.Domain/Calculations/MonthTaxAdjustment.cs`
- Create: `src/Firemka.Domain/Calculations/MonthSettlement.cs`
- Test: `tests/Firemka.Domain.Tests/MonthSettlementTests.cs`

**Interfaces:**
- Consumes: calculation IDs and fingerprints from Task 1.
- Produces: immutable declaration revisions, append-only adjustments, original close and correction lifecycle.

- [x] **Step 1: Write failing lifecycle tests**

```csharp
[Fact]
public void Zero_values_are_confirmed_inputs_and_revision_keeps_history()
{
    var first = Declaration(openingConfirmed: true, healthConfirmed: true);
    var second = first.CreateRevision(0m, 10m, -5m, 0m, 0m, true, true, "korekta testowa", Now);
    Assert.Equal(2, second.VersionNumber);
    Assert.Equal(first.Id, second.PreviousDeclarationId);
    Assert.Equal(10m, second.PitBaseAdjustment);
    Assert.Equal(0m, first.PitBaseAdjustment);
}

[Fact]
public void Correction_requires_reason_and_does_not_change_closed_version()
{
    var closed = MonthSettlement.CloseOriginal(CompanyId, "owner", Month, CalculationId, Fingerprint, Now);
    var correction = MonthSettlement.StartCorrection(closed, "spóźniona faktura", Now.AddDays(1));
    correction.Close(Calculation2Id, Fingerprint2, Now.AddDays(2));
    Assert.Equal(MonthSettlementStatus.Closed, closed.Status);
    Assert.Equal(closed.Id, correction.PreviousSettlementId);
    Assert.Equal(2, correction.VersionNumber);
}
```

Also test invalid signed/unsigned amounts, missing evidence, foreign adjustment without a document link, second close, correction from an open row and month/company mismatches.

- [x] **Step 2: Run and verify failure**

Run the new domain test filter. Expected: missing types.

- [x] **Step 3: Implement declaration revisions**

```csharp
public sealed class MonthDeclaration
{
    public static MonthDeclaration Create(
        Guid companyId, string ownerUserId, DateOnly month,
        decimal socialContributionsDeductible, decimal pitBaseAdjustment,
        decimal healthIncomeAdjustment, decimal? openingVatCarryForward,
        decimal? openingPitAdvancesDue, bool openingBalancesConfirmed,
        bool healthIncomeConfirmed, string evidenceReference, DateTimeOffset nowUtc);

    public MonthDeclaration CreateRevision(/* the same value fields */, DateTimeOffset nowUtc);
}
```

Store version/predecessor and a SHA-256 value fingerprint. Social/opening values are non-negative; PIT/health adjustments are signed. Evidence is mandatory. Revision does not mutate its predecessor.

- [x] **Step 4: Implement append-only adjustments**

```csharp
public enum MonthAdjustmentKind { PitRevenue, KpirCost, VatOutput, VatInput, HealthIncome, ForeignServiceVatOutput }
public sealed class MonthTaxAdjustment
{
    public static MonthTaxAdjustment Create(
        Guid companyId, string ownerUserId, DateOnly month, MonthAdjustmentKind kind,
        decimal amount, string reason, string evidenceReference,
        Guid? sourceDocumentId, Guid? salesInvoiceId, DateTimeOffset nowUtc);
}
```

Require a non-zero signed amount, reason/evidence, and at most one source link. `ForeignServiceVatOutput` requires `sourceDocumentId`. Rows have no edit/delete method.

- [x] **Step 5: Implement settlement versions**

```csharp
public enum MonthSettlementStatus { OpenCorrection = 1, Closed = 2 }
public sealed class MonthSettlement
{
    public static MonthSettlement CloseOriginal(
        Guid companyId, string ownerUserId, DateOnly month,
        Guid calculationId, string inputFingerprint, DateTimeOffset nowUtc);
    public static MonthSettlement StartCorrection(
        MonthSettlement previousClosed, string reason, DateTimeOffset nowUtc);
    public void Close(Guid calculationId, string inputFingerprint, DateTimeOffset nowUtc);
}
```

Original is version 1 and already closed. A correction is version N+1, points to the previous row and starts open. Closing rotates a concurrency stamp exactly once. Validate IDs/month/fingerprint/reason.

- [x] **Step 6: Run domain regression and checkpoint**

Run all domain tests. Expected: PASS. Check status; do not commit.

---

### Task 3: Owner-scoped calculation and closing workflow

**Files:**
- Create: `src/Firemka.Application/MonthClosing/MonthClosingContracts.cs`
- Create: `src/Firemka.Infrastructure/Calculations/MonthClosingService.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Firemka.Infrastructure/DependencyInjection.cs`
- Test: `tests/Firemka.Infrastructure.Tests/MonthClosingWorkflowTests.cs`

**Interfaces:**
- Consumes: company/tax-year profiles, `SalesInvoice`, KPiR/VAT entries, cost bookings and source documents.
- Produces: `IMonthClosingService.GetAsync`, `SaveDeclarationAsync`, `AddAdjustmentAsync`, `ConfirmRuleSetAsync`, `CloseAsync`, `StartCorrectionAsync`.

- [x] **Step 1: Write failing workflow tests**

Test these separate scenarios:

1. 2026 load creates exactly one `ReferenceOnly` rule and returns a technical preview.
2. Reference-only rules and absent declaration are named blockers; close writes nothing.
3. Ordinary issued sale + booked cost + confirmed declaration computes source totals and closes only after a confirmed rule revision.
4. No revenue and a loss produce zero PIT but still calculate minimum health.
5. First business month requires explicit zero/opening balances; later month derives prior VAT, PIT and health from the preceding closed calculation.
6. Unresolved source document, pending cost booking, draft/rejected/mismatch invoice and foreign service without linked VAT adjustment each have a stable blocker code/link.
7. `UnrelatedToBusiness` does not block.
8. Late invoice requires a linked evidenced adjustment.
9. Data added after close reports drift and cannot overwrite; correction reason creates version 2 and a new calculation.
10. Non-owner reads return null/404 semantics and writes throw `KeyNotFoundException`.
11. Invalid input or blocker leaves calculation/settlement counts unchanged.
12. Identical save/close/correction replays are idempotent.

- [x] **Step 2: Run and verify failure**

Run:

```bash
dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~MonthClosingWorkflowTests
```

Expected: compile failure for the contracts/service/DbSets.

- [x] **Step 3: Define application contracts**

Define records for rule, declaration, adjustment, explanation, calculation, settlement, blocker and view snapshots. Use stable blocker codes:

```text
TAX_YEAR_MISSING
RULE_SET_MISSING
RULE_SET_UNCONFIRMED
DECLARATION_MISSING
OPENING_BALANCES_UNCONFIRMED
HEALTH_INPUT_UNCONFIRMED
PREVIOUS_MONTH_OPEN
MONTH_NOT_ENDED
SOURCE_DOCUMENT_UNRESOLVED
COST_BOOKING_PENDING
SALES_INVOICE_MISSING
SALES_INVOICE_NOT_FINAL
SALES_CONTENT_MISMATCH
LATE_SALES_RECOGNITION_UNRESOLVED
FOREIGN_SERVICE_VAT_UNRESOLVED
CLOSED_INPUT_DRIFT
CORRECTION_REQUIRED
```

The interface is:

```csharp
public interface IMonthClosingService
{
    Task<MonthClosingView?> GetAsync(string ownerUserId, Guid companyId, DateOnly month, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<CalculationRuleSetSnapshot> ConfirmRuleSetAsync(string ownerUserId, Guid ruleSetId, ConfirmCalculationRuleSetCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<MonthDeclarationSnapshot> SaveDeclarationAsync(string ownerUserId, Guid companyId, DateOnly month, SaveMonthDeclarationCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<MonthAdjustmentSnapshot> AddAdjustmentAsync(string ownerUserId, Guid companyId, DateOnly month, AddMonthAdjustmentCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<MonthSettlementSnapshot> CloseAsync(string ownerUserId, Guid companyId, DateOnly month, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<MonthSettlementSnapshot> StartCorrectionAsync(string ownerUserId, Guid companyId, DateOnly month, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
```

- [x] **Step 4: Map persistence and uniqueness**

Add DbSets and mappings for all five entities plus calculation lines. Use `numeric(18,2)` for money and `numeric(5,2)` for rates, enum strings, owner max 450, evidence/source max 2,000 and SHA-256 max 64. Required unique indexes:

```csharp
(CompanyId, TaxYear, VersionNumber)
PreviousRuleSetId
(CompanyId, Month, VersionNumber) // declarations
PreviousDeclarationId
(CompanyId, Month, VersionNumber) // calculations
(CompanyId, Month, InputFingerprint)
(CompanyId, Month, VersionNumber) // settlements
PreviousSettlementId
```

All predecessor indexes are unique. Company/source FKs use `Restrict`; calculation lines may cascade only with their calculation. Mark settlement concurrency stamp as a token.

- [x] **Step 5: Implement source snapshot and blockers**

`GetAsync` verifies the company owner, normalizes month, and lazily creates one 2026 reference rule idempotently. For other years it returns `RULE_SET_MISSING`.

Use `SalesInvoice.ServiceMonth` for monthly sale selection, KPiR/VAT `Period` for ledger selection and the document `IssueDate` (or Warsaw-local `CreatedAtUtc` when absent) for unresolved-document selection. Every query includes `OwnerUserId` and company where available.

Build current-month source lines. Use the previous month’s latest closed calculation for cumulative PIT, prior advances, VAT carry and previous health income. The first business month uses the declaration opening values. Add all blockers from the contract list with direct links.

Preview may include draft sale amounts but must mark the sale non-final and cannot close. Closing rebuilds sources in a transaction and includes only an issued, non-mismatch sale.

- [x] **Step 6: Implement save, adjustment, confirmation, close and correction**

Declaration save returns the latest row when values match; otherwise creates the next revision. A closed latest settlement requires an open correction before declaration or adjustment writes.

Rule confirmation returns an existing identical confirmed successor or creates exactly one revision. Do not accept blank evidence/date outside the rule year context.

`CloseAsync` requires zero blockers, computes/persists one `MonthCalculation`, then either creates original settlement or closes the open correction in the same transaction. On a uniqueness/concurrency race, clear tracking and return only a stored settlement whose fingerprint proves the same operation; otherwise return a clear conflict.

`StartCorrectionAsync` requires a latest closed settlement and reason. Replays return the existing open successor. Later months detect a changed previous-calculation ID as drift.

Audit declaration, adjustment, rule confirmation, close and correction start without logging secrets or full source documents.

- [x] **Step 7: Run workflow tests**

Expected: every scenario PASS with no partial writes.

- [x] **Step 8: Generate and inspect migration**

Run:

```bash
dotnet ef migrations add Phase7MonthCalculationAndClosing --project src/Firemka.Infrastructure --startup-project src/Firemka.Web
```

Inspect `Up` and `Down`: only new Phase 7 tables/indexes/FKs; no unrelated drop or alteration. Run the pending-model check.

- [x] **Step 9: Add PostgreSQL proof**

Create `tests/Firemka.Infrastructure.Tests/Phase7PostgresTests.cs`. Migrate up/down/up in an isolated database, then prove concurrent identical rule reference creation, declaration save, close and correction start result in one chain. Prove different concurrent declarations do not silently overwrite and a failed close rolls back calculation plus settlement.

- [x] **Step 10: Run PostgreSQL and checkpoint**

Run with `FIREMKA_TEST_POSTGRES` against a dedicated temporary database. Expected: PASS and database removed afterward. Check status; do not commit.

---

### Task 4: Month, settlement and rule-confirmation pages

**Files:**
- Modify: `src/Firemka.Application/Month/MonthDashboardContracts.cs`
- Modify: `src/Firemka.Infrastructure/Companies/MonthDashboardQuery.cs`
- Modify: `src/Firemka.Web/Pages/Month/Index.cshtml`
- Modify: `src/Firemka.Web/Pages/Month/Index.cshtml.cs`
- Create: `src/Firemka.Web/Pages/Settlements/Month.cshtml`
- Create: `src/Firemka.Web/Pages/Settlements/Month.cshtml.cs`
- Create: `src/Firemka.Web/Pages/Settings/Calculations.cshtml`
- Create: `src/Firemka.Web/Pages/Settings/Calculations.cshtml.cs`
- Modify: `src/Firemka.Web/Pages/Settlements/Index.cshtml`
- Modify: `src/Firemka.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/Firemka.Web/wwwroot/css/site.css`
- Test: `tests/Firemka.Web.Tests/Phase7PagesTests.cs`

**Interfaces:**
- Consumes: `IMonthClosingService` snapshots only.
- Produces: responsive `/Month`, `/Settlements/Month`, `/Settings/Calculations` with antiforgery-protected commands.

- [x] **Step 1: Write failing page tests**

Test authenticated/anonymous access, owner isolation, visible reference-only warning, three estimate cards, blocker links, Polish decimal parsing, declaration validation preserving input, rule confirmation evidence, disabled close with blockers, successful close, drift/correction history and no government-send/payment action.

- [x] **Step 2: Run and verify failure**

Run the new web test class. Expected: missing routes/content.

- [x] **Step 3: Upgrade `Mój miesiąc`**

Keep the existing counts but fix document queries to include the owner. Add PIT, VAT and health values, `TechnicalPreview` warning, settlement status, next blocker and a link to details. Do not display unavailable values as zero.

- [x] **Step 4: Implement settlement details page**

Use four sections: `Źródła i wyjaśnienie`, `PIT, VAT i zdrowotna`, `Braki do rozwiązania`, `Zamknięcie i historia`. Add separate forms/handlers for declaration, adjustment, close and correction. The initial declaration form visibly asks the owner to confirm zero/opening values and the separate health bridge. Preserve validation errors and bound values.

- [x] **Step 5: Implement calculation-rules settings page**

Show all 2026 parameters, official source links, capture date and current trust state. Confirmation requires `Potwierdzam, że mam niezależną weryfikację`, evidence reference and date. POST creates a confirmed revision and redirects with a success message. Never expose a button that labels the built-in reference as confirmed without those inputs.

- [x] **Step 6: Run web tests**

Expected: PASS.

- [x] **Step 7: Run responsive browser acceptance**

Using an isolated Compose project and synthetic account/company, verify at 390 px and 1440 px:

- no horizontal page overflow;
- three values and technical warning are readable;
- blocker links work;
- declaration preserves comma decimals;
- rule confirmation creates version 2;
- ordinary synthetic month closes, then added synthetic adjustment shows drift and correction version 2;
- no action sends documents, pays liabilities or closes a year.

Save screenshots to `output/playwright/phase7-month-mobile.png`, `phase7-settlement-mobile.png`, `phase7-settlement-desktop.png` and `phase7-correction-mobile.png`. Remove only the isolated test containers/volumes afterward.

- [x] **Step 8: Run the complete Phase 7 gate**

Run all solution tests including PostgreSQL, Release build with zero warnings, format verification, EF pending-model check, vulnerable-package scan, four existing XSD fixtures and isolated Compose health. Confirm all Phase 0–6 regressions remain green.

- [x] **Step 9: Update active task documents**

Mark Phase 7 implemented only in its verified technical scope. Record test counts, migration name, source dates, screenshots, external independent-reference gate and the absence of commit/push/deploy. Set `Review fazy 7` to pending and hand the phase to `dev-docs-review`.

- [x] **Step 10: Record the final no-commit checkpoint**

Inspect `git status --short` and all generated migration files. Do not commit, push or deploy.
