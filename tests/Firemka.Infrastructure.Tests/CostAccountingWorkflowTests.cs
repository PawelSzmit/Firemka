using Firemka.Application.Accounting;
using Firemka.Domain.Accounting;
using Firemka.Domain.Companies;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Accounting;
using Firemka.Infrastructure.Auditing;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Tests;

public sealed class CostAccountingWorkflowTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public async Task First_document_waits_then_future_exact_match_books_once()
    {
        await using var db = CreateDbContext();
        var company = await SeedCompanyAsync(db);
        var firstDocument = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/1", "PL123", 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));

        var first = await service.PrepareAsync("owner-1", firstDocument.Id, Input(123m, 23m), Now);
        Assert.Equal(CostBookingStatus.PendingReview, first.Status);
        Assert.Empty(await db.KpirEntries.ToListAsync());

        await service.ConfirmAndApplyFutureAsync("owner-1", first.Id, Decision(), Now.AddMinutes(1));
        var secondDocument = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/2", "PL123", 246m, new DateOnly(2026, 9, 18));
        var second = await service.PrepareAsync("owner-1", secondDocument.Id, Input(246m, 46m), Now.AddDays(2));
        var replay = await service.PrepareAsync("owner-1", secondDocument.Id, Input(246m, 46m), Now.AddDays(2));

        Assert.Equal(company.Id, second.CompanyId);
        Assert.Equal(CostBookingStatus.BookedAutomatically, second.Status);
        Assert.Equal(second.Id, replay.Id);
        Assert.Equal(2, await db.KpirEntries.CountAsync());
        Assert.Equal(2, await db.VatPurchaseEntries.CountAsync());
        Assert.Equal(2, await db.CostBookings.CountAsync());
        Assert.Single(await db.CostRules.Where(item => item.IsActive).ToListAsync());
    }

    [Fact]
    public async Task Only_document_command_books_without_creating_a_rule()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var document = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/1", "PL123", 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));
        var booking = await service.PrepareAsync("owner-1", document.Id, Input(123m, 23m), Now);

        await service.ConfirmForDocumentAsync("owner-1", booking.Id, Decision(), Now.AddMinutes(1));

        Assert.Empty(await db.CostRules.ToListAsync());
        Assert.Single(await db.KpirEntries.ToListAsync());
        Assert.Single(await db.VatPurchaseEntries.ToListAsync());
        Assert.Equal(SourceDocumentStatus.Booked, (await db.SourceDocuments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Changed_country_waits_and_names_the_difference()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var firstDocument = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/1", "PL123", 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));
        var first = await service.PrepareAsync("owner-1", firstDocument.Id, Input(123m, 23m), Now);
        await service.ConfirmAndApplyFutureAsync("owner-1", first.Id, Decision(), Now.AddMinutes(1));

        var changed = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/2", "PL123", 123m, new DateOnly(2026, 9, 3));
        var pending = await service.PrepareAsync(
            "owner-1",
            changed.Id,
            Input(123m, 23m) with { SellerCountryCode = "DE" },
            Now.AddDays(1));
        var review = await service.GetReviewAsync("owner-1", changed.Id);

        Assert.Equal(CostBookingStatus.PendingReview, pending.Status);
        Assert.NotNull(review);
        Assert.Contains(review!.Differences, item => item.Contains("kraj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Non_owner_cannot_prepare_read_or_confirm()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var document = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/1", "PL123", 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));
        var booking = await service.PrepareAsync("owner-1", document.Id, Input(123m, 23m), Now);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.PrepareAsync("owner-2", document.Id, Input(123m, 23m), Now));
        Assert.Null(await service.GetReviewAsync("owner-2", document.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmForDocumentAsync("owner-2", booking.Id, Decision(), Now));
    }

    [Fact]
    public async Task Manual_period_rule_never_books_automatically()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var firstDocument = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/1", "PL123", 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));
        var first = await service.PrepareAsync("owner-1", firstDocument.Id, Input(123m, 23m), Now);
        await service.ConfirmAndApplyFutureAsync(
            "owner-1",
            first.Id,
            Decision() with { KpirPeriodPolicy = AccountingPeriodPolicy.Manual },
            Now.AddMinutes(1));
        var secondDocument = await SeedConfirmedDocumentAsync(db, "owner-1", "FV/2", "PL123", 123m, new DateOnly(2026, 9, 3));

        var second = await service.PrepareAsync("owner-1", secondDocument.Id, Input(123m, 23m), Now.AddDays(1));

        Assert.Equal(CostBookingStatus.PendingReview, second.Status);
    }

    [Fact]
    public async Task Seller_name_fallback_requires_explicit_confirmation_when_tax_id_is_missing()
    {
        await using var db = CreateDbContext();
        await SeedCompanyAsync(db);
        var document = await SeedConfirmedDocumentAsync(
            db, "owner-1", "FV/NO-NIP", string.Empty, 123m, new DateOnly(2026, 9, 2));
        var service = new CostAccountingService(db, new AuditTrail(db));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrepareAsync("owner-1", document.Id, Input(123m, 23m), Now));
        Assert.Contains("potwierdź", exception.Message, StringComparison.OrdinalIgnoreCase);

        var accepted = await service.PrepareAsync(
            "owner-1",
            document.Id,
            Input(123m, 23m) with { SellerNameFallbackConfirmed = true },
            Now);

        Assert.Equal(CostBookingStatus.PendingReview, accepted.Status);
        Assert.StartsWith("NAZWA", accepted.SellerKey, StringComparison.Ordinal);
    }

    private static PrepareCostCommand Input(decimal gross, decimal vat)
        => new("PL", VatTreatment.DomesticTaxed, 23m, CostServiceKind.Ai, gross, vat);

    private static CostDecisionCommand Decision()
        => new(
            "Pozostałe wydatki",
            50m,
            75m,
            AccountingPeriodPolicy.IssueMonth,
            AccountingPeriodPolicy.IssueMonth,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1),
            "sztuczna decyzja testowa");

    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cost-accounting-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Company> SeedCompanyAsync(AppDbContext db)
    {
        var company = Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowy adres 1, 00-001 Warszawa",
            new DateOnly(2026, 1, 1),
            "Testowy Klient",
            "1234563218",
            "Testowy adres 2, 00-002 Warszawa",
            "Testowa usługa",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            Now);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<SourceDocument> SeedConfirmedDocumentAsync(
        AppDbContext db,
        string owner,
        string invoiceNumber,
        string sellerTaxId,
        decimal gross,
        DateOnly issueDate)
    {
        var document = SourceDocument.CreateManual(Guid.NewGuid(), owner, Now);
        document.AttachSource(Guid.NewGuid(), new string('A', 64), Now);
        document.ApplyExtraction(DocumentData.Empty, null, Now);
        document.ConfirmData(new DocumentData(
            invoiceNumber,
            "Testowy Dostawca",
            sellerTaxId,
            issueDate,
            gross,
            "PLN"), Now);
        db.SourceDocuments.Add(document);
        await db.SaveChangesAsync();
        return document;
    }
}
