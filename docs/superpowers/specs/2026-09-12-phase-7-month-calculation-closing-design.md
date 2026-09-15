# Phase 7 Month Calculation and Closing Design

Date: 2026-09-12  
Status: approved by the standing instruction to execute every phase; implementation still requires the Phase 7 review gate

## Goal

Calculate one JDG month on the tax scale in a deterministic and explainable way, show every unresolved input, and preserve immutable month-closing versions. Technical estimates may use a reference-only rule set, but closing requires an independently confirmed rule version.

## Scope

Phase 7 covers:

- monthly business revenue and KPiR cost totals;
- cumulative PIT estimate for tax-scale business income;
- monthly output/input VAT and carry-forward;
- health-contribution estimate separated from PIT income;
- explicit monthly declarations and adjustments with evidence;
- blockers, preview, closing, drift detection and correction versions;
- integration with the existing `Mój miesiąc` and a detailed settlement page;
- synthetic and official-source reference cases, PostgreSQL concurrency and responsive browser acceptance.

It does not generate or send JPK/ZUS files, record payments, prepare the annual PIT, close a tax year, or enable production use without independent confirmation. Those remain in later phases.

## Trust model

`CalculationRuleSet` is immutable and versioned per company and tax year. It stores every numeric parameter, its official source references and capture date. It has one of two states:

- `ReferenceOnly`: usable for a clearly labelled technical preview;
- `IndependentlyConfirmed`: usable for closing only after the owner records an external confirmation reference and date.

The application contains a 2026 reference template from official sources, but never marks it independently confirmed. A new year has no guessed rules. A rule revision never changes an earlier calculation or closure.

Official sources checked on 2026-09-12:

- [MF — opodatkowanie według skali](https://www.podatki.gov.pl/podatki-firmowe/pit/informacje-podstawowe/co-jest-opodatkowane/opodatkowanie-wedlug-skali-podatkowej): 12% to 120,000 PLN, 32% above, 3,600 PLN tax-reducing amount, and the 1,000 PLN advance-payment option;
- [ZUS — health contribution calculator for 2026](https://www.zus.pl/pl/firmy/przedsiebiorco-przeczytaj-wazne/kalkulator-skladki-zdrowotnej): 9%, the previous-month income basis, 3,499.50 PLN minimum for January 2026 and 4,806 PLN from February 2026;
- [MF — VAT deduction and carry-forward](https://www.podatki.gov.pl/podatki-firmowe/vat/poradniki-i-informatory/odliczenie-i-zwrot-podatku-vat): output/input difference and transfer of an input-VAT surplus;
- [Ordynacja podatkowa, article 63](https://eli.gov.pl/api/acts/DU/2022/2651/text.html): whole-zloty rounding rule for tax bases and tax amounts.

These sources do not replace the required independent review of the owner’s concrete facts, opening balances, foreign purchases, car agreement or home-charging evidence.

## Domain model

### CalculationRuleSet

An immutable rule-set revision stores:

- company, owner, tax year, version and predecessor;
- lower threshold, lower/higher PIT rates and annual reducing amount;
- PIT payment-option threshold;
- health rate and month-effective minimum-base periods;
- source URLs, capture date, state and optional independent-confirmation reference/date;
- creation timestamp and input fingerprint.

Only one latest revision exists for a company/year. Confirmation creates a new revision instead of editing the reference row.

### MonthDeclaration

Each month has an immutable declaration revision with explicit values that cannot be safely inferred from invoices alone:

- social contributions paid and not already included in KPiR;
- PIT-base adjustment;
- health-income adjustment;
- opening VAT carry-forward for the first business month only;
- opening PIT advances for the first business month only;
- confirmation that the health bridge and opening values were checked;
- evidence/reference text, version and predecessor.

Zero is a real, confirmed value, not a missing value. A changed declaration creates a new revision.

### MonthTaxAdjustment

An append-only adjustment handles explicit exceptions and corrections without overwriting source records. It stores a month, type (`PitRevenue`, `KpirCost`, `VatOutput`, `VatInput`, `HealthIncome`), signed amount, reason, evidence reference and optional source-document or sales-invoice link. It is included in the calculation fingerprint and explanation lines.

Foreign-service documents never create output VAT silently. A booked cost with `ForeignService` blocks closing until it has an explicit, linked VAT-output adjustment or has been resolved by a later verified rule.

### MonthCalculation

Every persisted calculation is an immutable snapshot containing:

- exact rule/declaration revision IDs;
- source invoice, KPiR, VAT and adjustment fingerprints;
- monthly and year-to-date revenue, costs, income/loss and deductions;
- rounded PIT base, cumulative PIT, previous advances and current advance;
- output VAT, deductible input VAT, prior carry, payable VAT and next carry;
- separately derived health income, the previous-month basis, minimum basis and health contribution;
- one explanation line per source and formula;
- `TechnicalPreview` or `ClosingEligible` trust state.

PIT uses cumulative business values. The current advance is cumulative calculated tax less earlier advances due, never below zero. A result at or below the configured 1,000 PLN option is still shown as a liability together with `payment may be deferred`; the application does not silently choose deferral.

VAT uses cents for ledger totals and a separately displayed whole-zloty payment estimate. A surplus is carried to the next month. Phase 8 remains responsible for mapping exact values to the current JPK structure.

Health contribution uses the previous month’s separately confirmed health income, never aliases the PIT income field. The basis is at least the minimum applicable to the contribution month; the contribution is rounded to cents.

### MonthSettlement

A settlement version stores company, owner, month, version, predecessor, status, correction reason, calculation ID, input fingerprint and timestamps. Version 1 closes the original month. `StartCorrection(reason)` creates an open version linked to the prior closed version; reclosing attaches a new calculation and leaves every old version untouched.

The latest closed settlement is compared with current input. If data changed, the page shows drift and requires `Rozpocznij korektę`; it never overwrites or silently refreshes the closed snapshot.

## Input selection and blockers

Revenue comes only from issued sales invoices for the service month. Draft, sending, uncertain, rejected or content-mismatch sales invoices block closing. If issue month differs from service month, the technical estimate is visible but closing requires an explicit linked adjustment/evidence for recognition.

Costs come from included KPiR entries whose accounting period equals the selected month. Input VAT comes from included VAT-purchase entries for the selected VAT period. Unresolved source documents are assigned by confirmed issue month; when the issue date is unknown, their Warsaw-local intake month is used.

Closing blockers include at least:

- no tax year or unsupported taxation form;
- no calculation rule set;
- rule set not independently confirmed;
- no confirmed monthly declaration or required opening values;
- unresolved source document or pending cost booking;
- non-final or mismatched sales invoice;
- foreign-service VAT not explicitly resolved;
- missing previous closed month after the business start month;
- input drift while a closed version exists and no correction is open;
- concurrent update that won the same close/correction operation.

Every blocker has a stable code, Polish message and direct remediation link. `UnrelatedToBusiness` is a terminal explicit decision and does not block.

## Application flow

`IMonthClosingService.GetAsync` reads current sources, returns blockers and a technical preview when enough numeric input exists. `SaveDeclarationAsync` and `AddAdjustmentAsync` create append-only revisions/rows. `CloseAsync` rebuilds the input inside one transaction, requires zero blockers and an independently confirmed rule set, persists one calculation and one settlement version, and treats an identical concurrent close as an idempotent replay. `StartCorrectionAsync` requires a closed latest version and a non-empty reason.

The existing dashboard query consumes the month-closing read model and displays PIT, VAT, health, settlement status and the next blocking action. It remains owner-scoped.

## UI

`/Month` keeps the compact overview. It shows three amount cards, a trust label, closing status and the highest-priority blocker. Amounts from a reference-only rule set say `Szacunek techniczny — niezatwierdzone zasady`.

`/Settlements/Month?year=YYYY&month=MM` contains:

1. source totals and explanation lines;
2. PIT, VAT and health cards with formulas;
3. the monthly-declaration form;
4. blockers with links;
5. close or start-correction action;
6. immutable prior versions and visible differences.

`/Settings/Calculations` displays the 2026 reference values and source links. Independent confirmation requires a checkbox, reference text and date; it creates a confirmed revision. There is no one-click claim that official links alone prove the concrete business case.

## Errors and concurrency

Validation errors preserve inputs and use Polish messages. A parse, missing source, unsupported year, stale declaration, uniqueness conflict or changed calculation fingerprint cannot produce a partial close. PostgreSQL unique indexes cover company/year/version, predecessor links, company/month/declaration version, company/month/settlement version and one successor per immutable chain.

Repeated identical saves, close requests and correction starts return the winner’s stored state. Different simultaneous inputs return a clear refresh message.

## Verification

Unit tests cover both PIT brackets, losses, whole-zloty rounding, VAT payable/carry, minimum health basis, previous-month health income and immutable revisions. Workflow tests cover ordinary/no-revenue/loss/start-month/foreign-service/car-cost/late-invoice/correction scenarios, owner isolation, no partial close and input drift. PostgreSQL tests cover migrate down/up and concurrent declaration, close and correction races.

Reference fixtures are labelled synthetic or official-source-derived; none claims accountant approval. Browser checks use synthetic data at 390 px and 1440 px. The final gate also runs the full solution tests, Release build, format check, pending-model check, dependency scan, XSD fixtures and isolated Compose health.

## Acceptance boundary

The technical implementation is accepted when all automated and browser checks pass and no P1/P2 code finding remains. Actual month closing remains deliberately unavailable until the owner records independent confirmation of the rule set and the concrete opening/month inputs. This external gate is reported explicitly and cannot be closed by code.
