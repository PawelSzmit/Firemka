using Firemka.Domain.Calculations;

namespace Firemka.Domain.Tests;

public sealed class MonthSettlementTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CalculationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Calculation2Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly Month = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Fingerprint2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Zero_values_are_confirmed_inputs_and_revision_keeps_history()
    {
        var first = Declaration(openingConfirmed: true, healthConfirmed: true);

        var second = first.CreateRevision(
            0m,
            10m,
            -5m,
            0m,
            0m,
            openingBalancesConfirmed: true,
            healthIncomeConfirmed: true,
            "korekta testowa",
            Now.AddHours(1));

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.Id, second.PreviousDeclarationId);
        Assert.Equal(10m, second.PitBaseAdjustment);
        Assert.Equal(-5m, second.HealthIncomeAdjustment);
        Assert.Equal(0m, first.PitBaseAdjustment);
        Assert.NotEqual(first.ValueFingerprint, second.ValueFingerprint);
    }

    [Fact]
    public void Declaration_requires_evidence_and_confirmed_opening_values()
    {
        Assert.Throws<ArgumentException>(() => MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            0m,
            0m,
            0m,
            0m,
            0m,
            true,
            true,
            " ",
            Now));
        Assert.Throws<InvalidOperationException>(() => MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            0m,
            0m,
            0m,
            null,
            0m,
            true,
            true,
            "synthetic:evidence",
            Now));
    }

    [Fact]
    public void Declaration_rejects_negative_unsigned_values_but_accepts_signed_adjustments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            -1m,
            0m,
            0m,
            0m,
            0m,
            true,
            true,
            "synthetic:evidence",
            Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            0m,
            0m,
            0m,
            -1m,
            0m,
            true,
            true,
            "synthetic:evidence",
            Now));

        var declaration = MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            0m,
            -100m,
            -200m,
            null,
            null,
            false,
            true,
            "synthetic:evidence",
            Now);

        Assert.Equal(-100m, declaration.PitBaseAdjustment);
        Assert.Equal(-200m, declaration.HealthIncomeAdjustment);
    }

    [Fact]
    public void Adjustment_is_append_only_and_requires_reason_evidence_and_one_link_at_most()
    {
        var sourceId = Guid.NewGuid();
        var adjustment = MonthTaxAdjustment.Create(
            CompanyId,
            "owner-1",
            Month,
            MonthAdjustmentKind.KpirCost,
            -50m,
            "spóźniony koszt",
            "synthetic:document",
            sourceId,
            null,
            Now);

        Assert.Equal(-50m, adjustment.Amount);
        Assert.Equal(sourceId, adjustment.SourceDocumentId);
        Assert.Null(adjustment.SalesInvoiceId);
        Assert.Throws<ArgumentOutOfRangeException>(() => Adjustment(amount: 0m));
        Assert.Throws<ArgumentException>(() => Adjustment(reason: " "));
        Assert.Throws<ArgumentException>(() => Adjustment(evidence: " "));
        Assert.Throws<ArgumentException>(() => Adjustment(
            sourceDocumentId: Guid.NewGuid(),
            salesInvoiceId: Guid.NewGuid()));
    }

    [Fact]
    public void Foreign_service_vat_adjustment_requires_a_source_document_link()
    {
        Assert.Throws<InvalidOperationException>(() => Adjustment(
            kind: MonthAdjustmentKind.ForeignServiceVatOutput,
            sourceDocumentId: null));

        var adjustment = Adjustment(
            kind: MonthAdjustmentKind.ForeignServiceVatOutput,
            sourceDocumentId: Guid.NewGuid());

        Assert.Equal(MonthAdjustmentKind.ForeignServiceVatOutput, adjustment.Kind);
    }

    [Fact]
    public void Sales_recognition_is_a_zero_value_evidenced_decision_linked_to_one_invoice()
    {
        var invoiceId = Guid.NewGuid();

        var decision = Adjustment(
            amount: 0m,
            kind: MonthAdjustmentKind.SalesRecognition,
            salesInvoiceId: invoiceId);

        Assert.Equal(0m, decision.Amount);
        Assert.Equal(invoiceId, decision.SalesInvoiceId);
        Assert.Throws<InvalidOperationException>(() => Adjustment(
            amount: 1m,
            kind: MonthAdjustmentKind.SalesRecognition,
            salesInvoiceId: invoiceId));
        Assert.Throws<InvalidOperationException>(() => Adjustment(
            amount: 0m,
            kind: MonthAdjustmentKind.SalesRecognition,
            salesInvoiceId: null));
    }

    [Fact]
    public void Correction_requires_reason_and_does_not_change_closed_version()
    {
        var closed = MonthSettlement.CloseOriginal(
            CompanyId,
            "owner-1",
            Month,
            CalculationId,
            Fingerprint,
            Now);

        var correction = MonthSettlement.StartCorrection(closed, "spóźniona faktura", Now.AddDays(1));
        correction.Close(Calculation2Id, Fingerprint2, Now.AddDays(2));

        Assert.Equal(MonthSettlementStatus.Closed, closed.Status);
        Assert.Equal(CalculationId, closed.CalculationId);
        Assert.Equal(Fingerprint, closed.InputFingerprint);
        Assert.Equal(closed.Id, correction.PreviousSettlementId);
        Assert.Equal(2, correction.VersionNumber);
        Assert.Equal(Calculation2Id, correction.CalculationId);
        Assert.Equal(MonthSettlementStatus.Closed, correction.Status);
    }

    [Fact]
    public void Open_correction_closes_once_and_rotates_its_concurrency_stamp_once()
    {
        var closed = ClosedSettlement();
        var correction = MonthSettlement.StartCorrection(closed, "korekta testowa", Now.AddHours(1));
        var openStamp = correction.ConcurrencyStamp;

        correction.Close(Calculation2Id, Fingerprint2, Now.AddHours(2));
        var closedStamp = correction.ConcurrencyStamp;

        Assert.NotEqual(openStamp, closedStamp);
        Assert.Throws<InvalidOperationException>(() =>
            correction.Close(Guid.NewGuid(), Fingerprint, Now.AddHours(3)));
        Assert.Equal(closedStamp, correction.ConcurrencyStamp);
    }

    [Fact]
    public void Correction_cannot_start_from_an_open_row_or_without_a_reason()
    {
        var closed = ClosedSettlement();
        var open = MonthSettlement.StartCorrection(closed, "pierwsza korekta", Now.AddHours(1));

        Assert.Throws<ArgumentException>(() => MonthSettlement.StartCorrection(closed, " ", Now.AddHours(1)));
        Assert.Throws<InvalidOperationException>(() =>
            MonthSettlement.StartCorrection(open, "druga korekta", Now.AddHours(2)));
    }

    [Fact]
    public void Settlement_rejects_invalid_company_month_calculation_and_fingerprint()
    {
        Assert.Throws<ArgumentException>(() => MonthSettlement.CloseOriginal(
            Guid.Empty,
            "owner-1",
            Month,
            CalculationId,
            Fingerprint,
            Now));
        Assert.Throws<ArgumentException>(() => MonthSettlement.CloseOriginal(
            CompanyId,
            "owner-1",
            new DateOnly(2026, 9, 2),
            CalculationId,
            Fingerprint,
            Now));
        Assert.Throws<ArgumentException>(() => MonthSettlement.CloseOriginal(
            CompanyId,
            "owner-1",
            Month,
            Guid.Empty,
            Fingerprint,
            Now));
        Assert.Throws<ArgumentException>(() => MonthSettlement.CloseOriginal(
            CompanyId,
            "owner-1",
            Month,
            CalculationId,
            "bad",
            Now));
    }

    private static MonthDeclaration Declaration(bool openingConfirmed, bool healthConfirmed)
        => MonthDeclaration.Create(
            CompanyId,
            "owner-1",
            Month,
            socialContributionsDeductible: 0m,
            pitBaseAdjustment: 0m,
            healthIncomeAdjustment: 0m,
            openingVatCarryForward: openingConfirmed ? 0m : null,
            openingPitAdvancesDue: openingConfirmed ? 0m : null,
            openingBalancesConfirmed: openingConfirmed,
            healthIncomeConfirmed: healthConfirmed,
            evidenceReference: "synthetic:evidence",
            Now);

    private static MonthTaxAdjustment Adjustment(
        decimal amount = 1m,
        MonthAdjustmentKind kind = MonthAdjustmentKind.PitRevenue,
        string reason = "korekta testowa",
        string evidence = "synthetic:evidence",
        Guid? sourceDocumentId = null,
        Guid? salesInvoiceId = null)
        => MonthTaxAdjustment.Create(
            CompanyId,
            "owner-1",
            Month,
            kind,
            amount,
            reason,
            evidence,
            sourceDocumentId,
            salesInvoiceId,
            Now);

    private static MonthSettlement ClosedSettlement()
        => MonthSettlement.CloseOriginal(
            CompanyId,
            "owner-1",
            Month,
            CalculationId,
            Fingerprint,
            Now);
}
