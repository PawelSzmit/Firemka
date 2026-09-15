# Phase 6C Home Charging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import explicitly configured home-charging CSV files idempotently and produce immutable monthly energy-cost snapshots that never book tax ledgers automatically.

**Architecture:** `Firemka.Domain.Charging` owns validated profiles, sessions, import batches and report snapshots. It reuses the versioned `EnergyRatePeriod` already owned by `Company`, rather than creating a second source of truth. A bounded CSV parser validates the complete file before `IHomeChargingService` opens the write transaction. File and normalized-row hashes make replays safe; report-session links freeze historical inputs.

**Tech Stack:** .NET 10, C# 14, EF Core 10, PostgreSQL 18, ASP.NET Core Razor Pages, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-11-phase-6-accounting-rules-charging-design.md`

## Global Constraints

- A profile cannot activate until the owner explicitly confirms `Wh`.
- The parser accepts quoted CSV fields and reports a human-readable row number.
- The complete file is validated before any sessions are saved.
- A report is an immutable snapshot of exact sessions and one exact rate revision.
- No charging action writes `CostBooking`, `KpirEntry` or `VatPurchaseEntry`.
- No commit, push, deployment or real charging file in this execution.

---

### Task 1: Charging profile, session and report domain

**Files:**
- Create: `src/Firemka.Domain/Charging/ChargingCsvProfile.cs`
- Reuse: `src/Firemka.Domain/Companies/CompanySettingPeriods.cs` (`EnergyRatePeriod`)
- Create: `src/Firemka.Domain/Charging/ChargingSession.cs`
- Create: `src/Firemka.Domain/Charging/ChargingImportBatch.cs`
- Create: `src/Firemka.Domain/Charging/ChargingReport.cs`
- Test: `tests/Firemka.Domain.Tests/ChargingDomainTests.cs`

**Interfaces:**
- Produces validated profile activation, normalized-row sessions, replayable batches and versioned report snapshots tied to the existing effective-month energy-rate revision.

- [x] **Step 1: Write failing unit-confirmation, calculation and history tests**

```csharp
[Fact]
public void Profile_cannot_activate_until_wh_is_explicitly_confirmed()
{
    var profile = ChargingCsvProfile.Create(CompanyId, "owner", ';', "started", "energy", "yyyy-MM-dd HH:mm", "Europe/Warsaw", "session-id", Now);
    Assert.Throws<InvalidOperationException>(() => profile.Activate(whConfirmed: false, Now));
    profile.Activate(whConfirmed: true, Now);
    Assert.True(profile.IsActive);
}

[Fact]
public void Ten_thousand_wh_at_ninety_one_grosze_is_ten_kwh_and_nine_ten()
{
    var report = ChargingReport.Create(CompanyId, "owner", Month, rateId, 0.91m, new[] { Session(10_000m) }, version: 1, previousReportId: null, Now);
    Assert.Equal(10m, report.TotalKwh);
    Assert.Equal(9.10m, report.GrossCost);
    Assert.Equal("Tylko zestawienie — podstawa podatkowa niepotwierdzona", report.TaxStatus);
}
```

Add tests proving: profiles reject blank/duplicate column names, invalid separators/timezones/date formats and unconfirmed units; reports reject sessions from another company/month; a report revision points to its predecessor and leaves it unchanged. Existing `CompanyProfileTests` remain the authority that rates start on day one, reject invalid values and resolve by month.

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~ChargingDomainTests`

Expected: compile failure for missing charging types.

- [x] **Step 3: Implement profile invariants and reuse the existing rate model**

```csharp
public sealed class ChargingCsvProfile
{
    public static ChargingCsvProfile Create(Guid companyId, string ownerUserId, char separator, string timestampColumn, string energyWhColumn, string dateFormat, string timeZoneId, string? identityColumn, DateTimeOffset nowUtc);
    public void Activate(bool whConfirmed, DateTimeOffset nowUtc);
    public ChargingCsvProfile CreateRevision(char separator, string timestampColumn, string energyWhColumn, string dateFormat, string timeZoneId, string? identityColumn, DateTimeOffset nowUtc);
}

```

Validate `TimeZoneInfo.FindSystemTimeZoneById`, an exact parse using the declared format, unique required column names and a safe single-character separator. Use `Company.GetEnergyRate(month)` and its `EnergyRatePeriod.Id`, `ValidFromMonth` and `GrossPricePerKwh` directly; do not add a second rate table.

- [x] **Step 4: Implement session, batch and report snapshot invariants**

`ChargingSession` stores UTC start time, original local text, `EnergyWh`, normalized row hash and optional source identity. `ChargingImportBatch` stores file hash, counts and completion time. `ChargingReport` stores month, rate ID/value, version/previous ID, `TotalWh`, `TotalKwh`, `GrossCost`, tax status and a collection of immutable report-session links.

Use `decimal.Round(totalWh / 1000m, 3, MidpointRounding.AwayFromZero)` for displayed kWh and `decimal.Round(exactKwh * rate, 2, MidpointRounding.AwayFromZero)` for gross cost.

- [x] **Step 5: Run domain tests**

Run: `dotnet test tests/Firemka.Domain.Tests/Firemka.Domain.Tests.csproj --filter FullyQualifiedName~ChargingDomainTests`

Expected: PASS.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 2: Transaction-safe CSV parser

**Files:**
- Create: `src/Firemka.Application/Charging/ChargingContracts.cs`
- Create: `src/Firemka.Infrastructure/Charging/ChargingCsvParser.cs`
- Create: `tests/Firemka.Infrastructure.Tests/ChargingCsvParserTests.cs`
- Create: `tests/Fixtures/phase6/charging-synthetic.csv`

**Interfaces:**
- Produces: `IChargingCsvParser.ParseAsync(Stream, ChargingCsvProfileSnapshot, ChargingImportLimits, CancellationToken)` and either a complete validated result or a typed row error.

- [x] **Step 1: Write failing bounded-parser tests**

Write tests for: header mapping, quoted separator/newline fields, escaped quotes, Europe/Warsaw conversion to UTC, explicit Wh decimals, optional identity column, UTF-8 BOM, duplicate headers, missing columns, invalid date/energy, zero/negative energy, oversized bytes and excessive rows.

```csharp
[Fact]
public async Task One_invalid_row_returns_its_number_and_no_partial_result()
{
    var error = await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync("started;energy\n2026-09-01 01:00;1000\nwrong;2000"));
    Assert.Equal(3, error.RowNumber);
    Assert.Empty(error.AcceptedRows);
}
```

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~ChargingCsvParserTests`

Expected: compile failure for missing parser contracts.

- [x] **Step 3: Define explicit limits and result contracts**

```csharp
public sealed record ChargingImportLimits(long MaxBytes, int MaxRows)
{
    public static ChargingImportLimits Default => new(5 * 1024 * 1024, 50_000);
}

public sealed record ParsedChargingRow(int RowNumber, DateTimeOffset StartedAtUtc, string OriginalTimestamp, decimal EnergyWh, string? SourceIdentity, string NormalizedRowHash);
public sealed record ChargingCsvParseResult(string FileSha256, IReadOnlyList<ParsedChargingRow> Rows);
```

The file hash covers original bytes. The row hash covers canonical UTF-8 text made from UTC timestamp in round-trip format, invariant-decimal Wh and normalized optional identity, separated by an unambiguous control delimiter.

- [x] **Step 4: Implement full validation before returning**

Buffer no more than `MaxBytes + 1`, reject larger input, parse RFC-style quoted fields (including doubled quotes and embedded separators/newlines), validate exact headers, then parse every row into an in-memory result bounded by `MaxRows`. Formula-looking text remains text. Never evaluate or execute cell content.

- [x] **Step 5: Run parser tests and dependency audit**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~ChargingCsvParserTests`

Run: `dotnet list Firemka.slnx package --vulnerable --include-transitive`

Expected: parser PASS; no known vulnerable package.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 3: Import/report orchestration and persistence

**Files:**
- Extend: `src/Firemka.Application/Charging/ChargingContracts.cs`
- Create: `src/Firemka.Infrastructure/Charging/HomeChargingService.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/Firemka.Infrastructure/DependencyInjection.cs`
- Create: `tests/Firemka.Infrastructure.Tests/HomeChargingWorkflowTests.cs`

**Interfaces:**
- Produces: profile/rate commands, `IHomeChargingService.ImportAsync`, `GenerateReportAsync`, `GetMonthAsync`.

- [x] **Step 1: Write failing idempotence and immutable-report tests**

```csharp
[Fact]
public async Task Replayed_file_returns_existing_batch_and_overlap_adds_only_new_rows()
{
    var first = await service.ImportAsync("owner", CompanyId, profileId, File("a.csv"), Now);
    var replay = await service.ImportAsync("owner", CompanyId, profileId, File("a.csv"), Now);
    var overlap = await service.ImportAsync("owner", CompanyId, profileId, File("overlap.csv"), Now);
    Assert.Equal(first.BatchId, replay.BatchId);
    Assert.Equal(0, replay.AddedRows);
    Assert.Equal(1, overlap.AddedRows);
}

[Fact]
public async Task New_rate_does_not_change_an_existing_report()
{
    var original = await service.GenerateReportAsync("owner", CompanyId, Month, Now);
    await companyProfileService.ChangeEnergyRateAsync("owner", NextMonth, 1.10m, Now);
    var loaded = await service.GetReportAsync("owner", original.ReportId);
    Assert.Equal(0.91m, loaded.GrossRatePerKwh);
    Assert.Equal(9.10m, loaded.GrossCost);
}
```

Add tests proving: parse error writes no batch/session; missing active profile or month rate blocks the action; report regeneration with changed session set creates version 2; unchanged generation returns the same report; a non-owner cannot import/read/change; import/report never changes accounting ledger counts.

- [x] **Step 2: Run and verify failure**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~HomeChargingWorkflowTests`

Expected: compile failure for missing service and `DbSet` types.

- [x] **Step 3: Define service contracts**

```csharp
public interface IHomeChargingService
{
    Task<ChargingProfileSnapshot> SaveProfileAsync(string ownerUserId, Guid companyId, SaveChargingProfileCommand command, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<ChargingProfileSnapshot> ActivateProfileAsync(string ownerUserId, Guid profileId, bool whConfirmed, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<ChargingImportSnapshot> ImportAsync(string ownerUserId, Guid companyId, Guid profileId, Stream csv, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<ChargingReportSnapshot> GenerateReportAsync(string ownerUserId, Guid companyId, DateOnly month, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<ChargingMonthSnapshot> GetMonthAsync(string ownerUserId, Guid companyId, DateOnly month, CancellationToken cancellationToken = default);
    Task<ChargingReportSnapshot?> GetReportAsync(string ownerUserId, Guid reportId, CancellationToken cancellationToken = default);
}
```

- [x] **Step 4: Configure persistence uniqueness and transaction flow**

Add DbSets for profiles, sessions, batches, reports and report-session links. Reuse the existing `EnergyRatePeriod` owned by `Company`. Configure unique owner/company file hash, unique owner/company normalized row hash, unique report version per company/month and unique report/session link. The write transaction rechecks hashes after parsing; uniqueness races return the stored batch/session state.

`ImportAsync` sequence: verify owner/company/profile → parse entire file → begin transaction → replay check → insert only missing row hashes → insert completed batch/counts → commit. `GenerateReportAsync` selects the latest rate effective on/before month, freezes the session IDs/rate, and creates a new version only when the input fingerprint differs.

- [x] **Step 5: Implement and run workflow tests**

Run: `dotnet test tests/Firemka.Infrastructure.Tests/Firemka.Infrastructure.Tests.csproj --filter FullyQualifiedName~HomeChargingWorkflowTests`

Expected: PASS.

- [x] **Step 6: Record a no-commit checkpoint**

Check: `git status --short`; do not commit.

---

### Task 4: Charging page, migration, PostgreSQL and end-to-end acceptance

**Files:**
- Create: `src/Firemka.Web/Pages/Expenses/Charging.cshtml`
- Create: `src/Firemka.Web/Pages/Expenses/Charging.cshtml.cs`
- Modify: `src/Firemka.Web/Pages/Expenses/Index.cshtml`
- Modify: `src/Firemka.Web/Pages/Shared/_Layout.cshtml`
- Create: `tests/Firemka.Web.Tests/ChargingPageTests.cs`
- Create: `tests/Firemka.Infrastructure.Tests/PostgresHomeChargingTests.cs`
- Create: `src/Firemka.Infrastructure/Persistence/Migrations/*_HomeCharging.cs`
- Modify: `src/Firemka.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`

- [x] **Step 1: Write failing page tests**

Test anonymous redirect, owner isolation, profile activation blocked without Wh confirmation, invalid upload preserving no rows, successful import summary, rate validation, immutable report rendering and absence of every KPiR/VAT creation action.

- [x] **Step 2: Implement the Razor Page**

Provide four clear sections in Polish: `Profil pliku CSV`, `Stawka energii`, `Import sesji`, `Zestawienie miesiąca`. Require a confirmation checkbox labelled with `Wh`. Show row-specific validation errors and added/skipped counts. Show the exact warning `Tylko zestawienie — podstawa podatkowa niepotwierdzona`. Do not render a tax-booking button.

- [x] **Step 3: Run web tests**

Run: `dotnet test tests/Firemka.Web.Tests/Firemka.Web.Tests.csproj --filter FullyQualifiedName~ChargingPageTests`

Expected: PASS.

- [x] **Step 4: Generate and inspect migration**

Run: `dotnet ef migrations add HomeCharging --project src/Firemka.Infrastructure --startup-project src/Firemka.Web`

Inspect precision, foreign keys, cascading behavior and all hashes/version uniqueness. Reject unrelated schema drift.

- [x] **Step 5: Prove PostgreSQL replay/concurrency behavior**

Using only `FIREMKA_TEST_POSTGRES`, prove migration up/down/up, concurrent same-file import, overlapping-row import, report version uniqueness and rollback on invalid data. Confirm the user-facing Compose database was not altered by the dedicated test.

- [x] **Step 6: Run browser acceptance on synthetic data**

At 390 px and 1440 px, create/activate a synthetic profile, add `0.91 zł/kWh`, import `tests/Fixtures/phase6/charging-synthetic.csv`, generate the reference report and verify `10 kWh` / `9,10 zł`. Check no horizontal page overflow and confirm there is no KPiR/VAT action.

- [x] **Step 7: Run the complete Phase 6 gate**

Run all solution tests (including the conditional PostgreSQL suite), Release build with zero warnings, format verification, EF migration check, vulnerable-package scan, XSD/schema validation and Compose health check. Confirm Phase 5 tests remain green.

- [x] **Step 8: Update active task documents and record a no-commit checkpoint**

Update the Phase 6 section in `docs/active/firemka-jdg/firemka-jdg-plan.md`, `firemka-jdg-zadania.md` and `firemka-jdg-kontekst.md` with implementation evidence and the external real-data/tax gate. Check `git status --short`; do not commit, push or deploy.
