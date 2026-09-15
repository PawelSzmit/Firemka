using Firemka.Application.MonthClosing;
using Firemka.Domain.Accounting;
using Firemka.Domain.Calculations;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Domain.Sales;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Calculations;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class MonthClosingWorkflowTests
{
    private static readonly DateOnly September = new(2026, 9, 1);
    private static readonly DateTimeOffset OctoberNow = DateTimeOffset.Parse("2026-10-02T08:00:00Z");

    [Fact]
    public async Task First_2026_load_creates_one_reference_rule_and_returns_a_technical_preview()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        var service = CreateService(db);

        var first = await service.GetAsync("owner-1", company.Id, September, OctoberNow);
        var replay = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        Assert.NotNull(first);
        Assert.NotNull(first!.Calculation);
        Assert.Equal(CalculationTrust.TechnicalPreview, first.Calculation!.Trust);
        Assert.Equal(CalculationRuleTrust.ReferenceOnly, first.RuleSet!.Trust);
        Assert.Equal(first.RuleSet.Id, replay!.RuleSet!.Id);
        Assert.Single(await db.CalculationRuleSets.ToListAsync());
        Assert.Contains(first.Blockers, item => item.Code == MonthClosingBlockerCodes.RuleSetUnconfirmed);
        Assert.Contains(first.Blockers, item => item.Code == MonthClosingBlockerCodes.DeclarationMissing);
    }

    [Fact]
    public async Task Missing_declaration_and_reference_only_rules_block_close_without_partial_rows()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        var service = CreateService(db);
        await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        var error = await Assert.ThrowsAsync<MonthClosingBlockedException>(() =>
            service.CloseAsync("owner-1", company.Id, September, OctoberNow));

        Assert.Contains(error.Blockers, item => item.Code == MonthClosingBlockerCodes.RuleSetUnconfirmed);
        Assert.Contains(error.Blockers, item => item.Code == MonthClosingBlockerCodes.DeclarationMissing);
        Assert.Empty(await db.MonthCalculations.ToListAsync());
        Assert.Empty(await db.MonthSettlements.ToListAsync());
    }

    [Fact]
    public async Task Issued_sale_booked_cost_and_confirmed_inputs_close_once_with_source_totals()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        await SeedBookedCostAsync(db, company, September, gross: 1_230m, inputVat: 230m);
        var service = CreateService(db);
        await ConfirmRulesAsync(service, company.Id);
        await SaveDeclarationAsync(service, company.Id, September, opening: true);

        var closed = await service.CloseAsync("owner-1", company.Id, September, OctoberNow);
        var replay = await service.CloseAsync("owner-1", company.Id, September, OctoberNow);
        var view = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        Assert.Equal(closed.Id, replay.Id);
        Assert.Equal(MonthSettlementStatus.Closed, closed.Status);
        Assert.Equal(CalculationTrust.ClosingEligible, view!.Calculation!.Trust);
        Assert.Equal(10_000m, view.Calculation.RevenueMonth);
        Assert.Equal(1_000m, view.Calculation.CostsMonth);
        Assert.Equal(2_300m, view.Calculation.OutputVatMonth);
        Assert.Equal(230m, view.Calculation.InputVatMonth);
        Assert.Single(await db.MonthCalculations.ToListAsync());
        Assert.Single(await db.MonthSettlements.ToListAsync());
    }

    [Fact]
    public async Task No_revenue_and_a_loss_still_calculate_zero_pit_and_minimum_health()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedBookedCostAsync(db, company, September, gross: 1_230m, inputVat: 230m);
        var service = CreateService(db);
        await SaveDeclarationAsync(service, company.Id, September, opening: true);

        var view = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        Assert.NotNull(view!.Calculation);
        Assert.Equal(-1_000m, view.Calculation!.PitIncomeYtd);
        Assert.Equal(0m, view.Calculation.PitAdvanceDue);
        Assert.Equal(4_806m, view.Calculation.HealthBasis);
        Assert.Equal(432.54m, view.Calculation.HealthContribution);
        Assert.Contains(view.Blockers, item => item.Code == MonthClosingBlockerCodes.SalesInvoiceMissing);
    }

    [Fact]
    public async Task Later_month_derives_previous_vat_pit_and_health_from_the_closed_calculation()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        var service = CreateService(db);
        await ConfirmRulesAsync(service, company.Id);
        await SaveDeclarationAsync(
            service,
            company.Id,
            September,
            opening: true,
            openingVat: 3_000m,
            openingPit: 200m);
        await service.CloseAsync("owner-1", company.Id, September, OctoberNow);

        var october = September.AddMonths(1);
        await SeedIssuedSaleAsync(db, company, october, october.AddDays(1), 2_000m);
        await SaveDeclarationAsync(service, company.Id, october, opening: false);
        var octoberViewBeforeClose = await service.GetAsync(
            "owner-1",
            company.Id,
            october,
            DateTimeOffset.Parse("2026-11-02T08:00:00Z"));
        var octoberClosed = await service.CloseAsync(
            "owner-1",
            company.Id,
            october,
            DateTimeOffset.Parse("2026-11-02T08:00:00Z"));

        Assert.DoesNotContain(
            octoberViewBeforeClose!.Blockers,
            item => item.Code == MonthClosingBlockerCodes.PreviousMonthOpen);
        Assert.Equal(10_000m, octoberViewBeforeClose.Calculation!.PreviousMonthHealthIncome);
        Assert.Equal(10_000m, octoberViewBeforeClose.Calculation.HealthBasis);
        Assert.Equal(900m, octoberViewBeforeClose.Calculation.HealthContribution);
        Assert.Equal(700m, octoberViewBeforeClose.Calculation.PriorVatCarryForward);
        Assert.Equal(MonthSettlementStatus.Closed, octoberClosed.Status);
    }

    [Fact]
    public async Task Unresolved_document_pending_cost_and_draft_sale_have_stable_linked_blockers()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        db.SourceDocuments.Add(SourceDocument.CreateManual(
            Guid.NewGuid(),
            "owner-1",
            DateTimeOffset.Parse("2026-09-10T08:00:00Z")));
        await SeedPendingCostAsync(db, company, September, VatTreatment.DomesticTaxed);
        db.SalesInvoices.Add(SalesInvoice.CreateDraft(
            company.Id,
            "owner-1",
            September,
            company.SubscriptionRates.Single().Id,
            "Testowa usługa",
            1_000m,
            23m,
            OctoberNow));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var view = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        AssertBlocker(view!, MonthClosingBlockerCodes.SourceDocumentUnresolved, "/Invoices/Incoming");
        AssertBlocker(view!, MonthClosingBlockerCodes.CostBookingPending, "/Expenses/Review");
        AssertBlocker(view!, MonthClosingBlockerCodes.SalesInvoiceNotFinal, "/Invoices/Sales");
    }

    [Fact]
    public async Task Unrelated_document_does_not_block_but_content_mismatch_and_foreign_vat_do()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        var unrelated = CreateReviewedDocument("UNRELATED", September, 100m);
        unrelated.MarkUnrelated("prywatny zakup testowy", OctoberNow);
        db.SourceDocuments.Add(unrelated);
        await SeedMismatchedSaleAsync(db, company, September);
        var foreign = await SeedBookedCostAsync(
            db,
            company,
            September,
            gross: 1_000m,
            inputVat: 0m,
            VatTreatment.ForeignService);
        var service = CreateService(db);

        var before = await service.GetAsync("owner-1", company.Id, September, OctoberNow);
        Assert.DoesNotContain(before!.Blockers, item =>
            item.Code == MonthClosingBlockerCodes.SourceDocumentUnresolved
            && item.Message.Contains("UNRELATED", StringComparison.Ordinal));
        Assert.Contains(before.Blockers, item => item.Code == MonthClosingBlockerCodes.SalesContentMismatch);
        Assert.Contains(before.Blockers, item => item.Code == MonthClosingBlockerCodes.ForeignServiceVatUnresolved);

        await service.AddAdjustmentAsync(
            "owner-1",
            company.Id,
            September,
            new AddMonthAdjustmentCommand(
                MonthAdjustmentKind.ForeignServiceVatOutput,
                230m,
                "VAT należny od importu usługi",
                "synthetic:foreign-vat",
                foreign.SourceDocumentId,
                null),
            OctoberNow);
        var after = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        Assert.DoesNotContain(after!.Blockers, item => item.Code == MonthClosingBlockerCodes.ForeignServiceVatUnresolved);
    }

    [Fact]
    public async Task Late_sale_requires_a_linked_evidenced_adjustment()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        var invoice = await SeedIssuedSaleAsync(
            db,
            company,
            September,
            new DateOnly(2026, 10, 2),
            10_000m);
        var service = CreateService(db);

        var before = await service.GetAsync("owner-1", company.Id, September, OctoberNow);
        Assert.Contains(before!.Blockers, item => item.Code == MonthClosingBlockerCodes.LateSalesRecognitionUnresolved);

        await service.AddAdjustmentAsync(
            "owner-1",
            company.Id,
            September,
            new AddMonthAdjustmentCommand(
                MonthAdjustmentKind.HealthIncome,
                1m,
                "korekta innego obszaru",
                "synthetic:not-a-recognition-decision",
                null,
                invoice.Id),
            OctoberNow);
        var wrongKind = await service.GetAsync("owner-1", company.Id, September, OctoberNow);
        Assert.Contains(
            wrongKind!.Blockers,
            item => item.Code == MonthClosingBlockerCodes.LateSalesRecognitionUnresolved);

        await service.AddAdjustmentAsync(
            "owner-1",
            company.Id,
            September,
            new AddMonthAdjustmentCommand(
                MonthAdjustmentKind.SalesRecognition,
                0m,
                "rozpoznanie sprzedaży w miesiącu usługi",
                "synthetic:late-sale-recognition",
                null,
                invoice.Id),
            OctoberNow);
        var after = await service.GetAsync("owner-1", company.Id, September, OctoberNow);

        Assert.DoesNotContain(after!.Blockers, item => item.Code == MonthClosingBlockerCodes.LateSalesRecognitionUnresolved);
        Assert.Equal(10_000m, after.Calculation!.RevenueMonth);
    }

    [Fact]
    public async Task Unsupported_vat_and_zus_profiles_have_stable_linked_blockers()
    {
        await using var db = CreateDbContext();
        var october = September.AddMonths(1);
        var company = await SeedCompanyAsync(
            db,
            September,
            configuredCompany =>
            {
                configuredCompany.ChangeVatProfile(october, VatProfile.VatExempt, OctoberNow);
                configuredCompany.ChangeZusProfile(october, ZusProfile.SocialAndHealthContributions, OctoberNow);
            });
        var service = CreateService(db);

        var view = await service.GetAsync(
            "owner-1",
            company.Id,
            october,
            DateTimeOffset.Parse("2026-11-02T08:00:00Z"));

        AssertBlocker(view!, MonthClosingBlockerCodes.VatProfileUnsupported, "/Settings#vat");
        AssertBlocker(view!, MonthClosingBlockerCodes.ZusProfileUnsupported, "/Settings#zus");
    }

    [Fact]
    public async Task Booked_source_conflict_blocks_and_becomes_drift_after_an_earlier_close()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        var booking = await SeedBookedCostAsync(db, company, September, gross: 123m, inputVat: 23m);
        var service = CreateService(db);
        await ConfirmRulesAsync(service, company.Id);
        await SaveDeclarationAsync(service, company.Id, September, opening: true);
        await service.CloseAsync("owner-1", company.Id, September, OctoberNow);

        var document = await db.SourceDocuments.SingleAsync(item => item.Id == booking.SourceDocumentId);
        document.MarkSourceConflict(OctoberNow.AddMinutes(1));
        db.Entry(document.SourceConflicts.Single()).State = EntityState.Added;
        await db.SaveChangesAsync();

        var view = await service.GetAsync("owner-1", company.Id, September, OctoberNow.AddMinutes(2));

        AssertBlocker(view!, MonthClosingBlockerCodes.SourceDocumentUnresolved, "/Invoices/Incoming");
        Assert.True(view!.HasInputDrift);
        AssertBlocker(view, MonthClosingBlockerCodes.ClosedInputDrift, "/Settlements/Month");

        document.ResolveSourceConflict(
            "Porównano źródła; zachowany dokument jest prawidłowy.",
            OctoberNow.AddMinutes(3));
        await db.SaveChangesAsync();

        var resolved = await service.GetAsync(
            "owner-1",
            company.Id,
            September,
            OctoberNow.AddMinutes(4));

        Assert.DoesNotContain(
            resolved!.Blockers,
            item => item.Code == MonthClosingBlockerCodes.SourceDocumentUnresolved);
        Assert.False(resolved.HasInputDrift);
    }

    [Fact]
    public async Task Changed_sources_after_close_require_an_immutable_correction_version()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        var service = CreateService(db);
        await ConfirmRulesAsync(service, company.Id);
        await SaveDeclarationAsync(service, company.Id, September, opening: true);
        var original = await service.CloseAsync("owner-1", company.Id, September, OctoberNow);

        await SeedBookedCostAsync(db, company, September, gross: 123m, inputVat: 23m);
        var drift = await service.GetAsync("owner-1", company.Id, September, OctoberNow.AddHours(1));

        Assert.True(drift!.HasInputDrift);
        Assert.Contains(drift.Blockers, item => item.Code == MonthClosingBlockerCodes.ClosedInputDrift);
        await Assert.ThrowsAsync<MonthClosingBlockedException>(() =>
            service.CloseAsync("owner-1", company.Id, September, OctoberNow.AddHours(1)));

        var openCorrection = await service.StartCorrectionAsync(
            "owner-1",
            company.Id,
            September,
            "spóźniony dokument kosztowy",
            OctoberNow.AddHours(2));
        var corrected = await service.CloseAsync(
            "owner-1",
            company.Id,
            September,
            OctoberNow.AddHours(3));

        Assert.Equal(MonthSettlementStatus.OpenCorrection, openCorrection.Status);
        Assert.Equal(2, corrected.VersionNumber);
        Assert.Equal(original.Id, corrected.PreviousSettlementId);
        Assert.Equal(2, await db.MonthCalculations.CountAsync());
        Assert.Equal(2, await db.MonthSettlements.CountAsync());
    }

    [Fact]
    public async Task Non_owner_has_null_read_and_not_found_write_semantics()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        var service = CreateService(db);

        Assert.Null(await service.GetAsync("owner-2", company.Id, September, OctoberNow));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SaveDeclarationAsync(
            "owner-2",
            company.Id,
            September,
            Declaration(opening: true),
            OctoberNow));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddAdjustmentAsync(
            "owner-2",
            company.Id,
            September,
            new AddMonthAdjustmentCommand(
                MonthAdjustmentKind.PitRevenue,
                1m,
                "test",
                "synthetic:test",
                null,
                null),
            OctoberNow));
    }

    [Fact]
    public async Task Invalid_adjustment_and_blocked_close_leave_counts_unchanged()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAdjustmentAsync(
            "owner-1",
            company.Id,
            September,
            new AddMonthAdjustmentCommand(
                MonthAdjustmentKind.KpirCost,
                -1m,
                "ujemny koszt bez źródła",
                "synthetic:invalid",
                null,
                null),
            OctoberNow));
        await Assert.ThrowsAsync<MonthClosingBlockedException>(() =>
            service.CloseAsync("owner-1", company.Id, September, OctoberNow));

        Assert.Empty(await db.MonthTaxAdjustments.ToListAsync());
        Assert.Empty(await db.MonthCalculations.ToListAsync());
        Assert.Empty(await db.MonthSettlements.ToListAsync());
    }

    [Fact]
    public async Task Identical_declaration_confirmation_and_correction_replays_return_the_winner()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db, September);
        await SeedIssuedSaleAsync(db, company, September, September.AddDays(1), 10_000m);
        var service = CreateService(db);
        var firstView = await service.GetAsync("owner-1", company.Id, September, OctoberNow);
        var confirmation = new ConfirmCalculationRuleSetCommand(
            true,
            "synthetic:independent-review",
            new DateOnly(2026, 9, 12));

        var firstRule = await service.ConfirmRuleSetAsync(
            "owner-1", firstView!.RuleSet!.Id, confirmation, OctoberNow);
        var repeatedRule = await service.ConfirmRuleSetAsync(
            "owner-1", firstView.RuleSet.Id, confirmation, OctoberNow);
        var firstDeclaration = await service.SaveDeclarationAsync(
            "owner-1", company.Id, September, Declaration(opening: true), OctoberNow);
        var repeatedDeclaration = await service.SaveDeclarationAsync(
            "owner-1", company.Id, September, Declaration(opening: true), OctoberNow);
        await service.CloseAsync("owner-1", company.Id, September, OctoberNow);
        var firstCorrection = await service.StartCorrectionAsync(
            "owner-1", company.Id, September, "korekta testowa", OctoberNow.AddHours(1));
        var repeatedCorrection = await service.StartCorrectionAsync(
            "owner-1", company.Id, September, "korekta testowa", OctoberNow.AddHours(1));

        Assert.Equal(firstRule.Id, repeatedRule.Id);
        Assert.Equal(firstDeclaration.Id, repeatedDeclaration.Id);
        Assert.Equal(firstCorrection.Id, repeatedCorrection.Id);
        Assert.Equal(2, await db.CalculationRuleSets.CountAsync());
        Assert.Single(await db.MonthDeclarations.ToListAsync());
        Assert.Equal(2, await db.MonthSettlements.CountAsync());
    }

    private static MonthClosingService CreateService(AppDbContext db)
        => new(db, new AuditTrail(db));

    private static AppDbContext CreateDbContext(string? name = null)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? $"month-closing-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Company> SeedCompanyAsync(
        AppDbContext db,
        DateOnly businessStart,
        Action<Company>? configure = null)
    {
        var company = Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowy adres 1, 00-001 Warszawa",
            businessStart,
            "Testowy Klient",
            "1234563218",
            "Testowy adres 2, 00-002 Warszawa",
            "Testowa usługa",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            OctoberNow);
        configure?.Invoke(company);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<SalesInvoice> SeedIssuedSaleAsync(
        AppDbContext db,
        Company company,
        DateOnly serviceMonth,
        DateOnly issueDate,
        decimal net)
    {
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            company.OwnerUserId,
            serviceMonth,
            company.GetSubscriptionRate(serviceMonth).Id,
            company.ServiceDescription,
            net,
            23m,
            OctoberNow);
        invoice.PrepareForIssue(issueDate, $"FV/{serviceMonth:yyyyMM}", false, "<Invoice />", OctoberNow);
        invoice.RegisterSubmission("session-test", $"submission-{serviceMonth:yyyyMM}", OctoberNow);
        invoice.MarkIssued(
            $"KSEF-{serviceMonth:yyyyMM}",
            "<UPO />",
            "<Invoice />",
            OctoberNow);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task SeedMismatchedSaleAsync(AppDbContext db, Company company, DateOnly month)
    {
        var invoice = SalesInvoice.CreateDraft(
            company.Id,
            company.OwnerUserId,
            month,
            company.GetSubscriptionRate(month).Id,
            company.ServiceDescription,
            1_000m,
            23m,
            OctoberNow);
        invoice.PrepareForIssue(month.AddDays(1), "FV/MISMATCH", false, "<Invoice />", OctoberNow);
        invoice.RegisterSubmission("session-test", "submission-mismatch", OctoberNow);
        invoice.MarkIssuedContentMismatch("KSEF-MISMATCH", "<UPO />", "<Different />", OctoberNow);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
    }

    private static async Task<CostBooking> SeedBookedCostAsync(
        AppDbContext db,
        Company company,
        DateOnly month,
        decimal gross,
        decimal inputVat,
        VatTreatment treatment = VatTreatment.DomesticTaxed)
    {
        var document = CreateReviewedDocument($"K/{Guid.NewGuid():N}", month, gross);
        var booking = CostBooking.CreatePending(
            document.Id,
            company.Id,
            company.OwnerUserId,
            CostDocumentFingerprint.Create(
                "PL123",
                treatment == VatTreatment.ForeignService ? "DE" : "PL",
                "PLN",
                treatment,
                treatment == VatTreatment.DomesticTaxed ? 23m : null,
                CostServiceKind.Ai),
            gross,
            inputVat,
            month,
            OctoberNow);
        var calculation = booking.Book(
            null,
            "Pozostałe wydatki",
            inputVat == 0m ? 0m : 100m,
            100m,
            month,
            month,
            "synthetic:booking",
            false,
            OctoberNow);
        document.TransitionTo(SourceDocumentStatus.Booked, OctoberNow);
        db.SourceDocuments.Add(document);
        db.CostBookings.Add(booking);
        db.KpirEntries.Add(KpirEntry.Create(
            booking.Id,
            document.Id,
            company.OwnerUserId,
            month,
            "Pozostałe wydatki",
            calculation.KpirAmount,
            null,
            OctoberNow));
        db.VatPurchaseEntries.Add(VatPurchaseEntry.Create(
            booking.Id,
            document.Id,
            company.OwnerUserId,
            month,
            inputVat,
            calculation.DeductibleVatAmount,
            null,
            OctoberNow));
        await db.SaveChangesAsync();
        return booking;
    }

    private static async Task<CostBooking> SeedPendingCostAsync(
        AppDbContext db,
        Company company,
        DateOnly month,
        VatTreatment treatment)
    {
        var document = CreateReviewedDocument($"P/{Guid.NewGuid():N}", month, 123m);
        var booking = CostBooking.CreatePending(
            document.Id,
            company.Id,
            company.OwnerUserId,
            CostDocumentFingerprint.Create(
                "PL123",
                "PL",
                "PLN",
                treatment,
                treatment == VatTreatment.DomesticTaxed ? 23m : null,
                CostServiceKind.Other),
            123m,
            treatment == VatTreatment.DomesticTaxed ? 23m : 0m,
            month,
            OctoberNow);
        db.SourceDocuments.Add(document);
        db.CostBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    private static SourceDocument CreateReviewedDocument(string number, DateOnly issueDate, decimal gross)
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), "owner-1", OctoberNow);
        document.AttachSource(Guid.NewGuid(), new string('A', 64), OctoberNow);
        document.ApplyExtraction(DocumentData.Empty, null, OctoberNow);
        document.ConfirmData(new DocumentData(
            number,
            "Testowy dostawca",
            "PL123",
            issueDate,
            gross,
            "PLN"), OctoberNow);
        return document;
    }

    private static async Task<CalculationRuleSetSnapshot> ConfirmRulesAsync(
        MonthClosingService service,
        Guid companyId)
    {
        var view = await service.GetAsync("owner-1", companyId, September, OctoberNow);
        return await service.ConfirmRuleSetAsync(
            "owner-1",
            view!.RuleSet!.Id,
            new ConfirmCalculationRuleSetCommand(
                true,
                "synthetic:independent-review",
                new DateOnly(2026, 9, 12)),
            OctoberNow);
    }

    private static Task<MonthDeclarationSnapshot> SaveDeclarationAsync(
        MonthClosingService service,
        Guid companyId,
        DateOnly month,
        bool opening,
        decimal openingVat = 0m,
        decimal openingPit = 0m)
        => service.SaveDeclarationAsync(
            "owner-1",
            companyId,
            month,
            Declaration(opening, openingVat, openingPit),
            OctoberNow);

    private static SaveMonthDeclarationCommand Declaration(
        bool opening,
        decimal openingVat = 0m,
        decimal openingPit = 0m)
        => new(
            SocialContributionsDeductible: 0m,
            PitBaseAdjustment: 0m,
            HealthIncomeAdjustment: 0m,
            OpeningVatCarryForward: opening ? openingVat : null,
            OpeningPitAdvancesDue: opening ? openingPit : null,
            OpeningBalancesConfirmed: opening,
            HealthIncomeConfirmed: true,
            EvidenceReference: "synthetic:monthly-declaration");

    private static void AssertBlocker(MonthClosingView view, string code, string expectedLink)
    {
        var blocker = Assert.Single(view.Blockers, item => item.Code == code);
        Assert.StartsWith(expectedLink, blocker.ActionUrl, StringComparison.Ordinal);
    }
}
