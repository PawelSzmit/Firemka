using Firemka.Domain.Accounting;

namespace Firemka.Domain.Tests;

public sealed class CostBookingTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly IssueDate = new(2026, 9, 12);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public void Booking_calculates_vat_then_kpir_and_rounds_final_results_to_grosze()
    {
        var booking = CostBooking.CreatePending(
            DocumentId,
            CompanyId,
            "owner-1",
            Fingerprint(),
            123m,
            23m,
            IssueDate,
            Now);

        var calculation = booking.Book(
            ruleId: null,
            "Pozostałe wydatki",
            50m,
            75m,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1),
            "decyzja właściciela",
            automatic: false,
            Now);

        Assert.Equal(11.50m, calculation.DeductibleVatAmount);
        Assert.Equal(83.63m, calculation.KpirAmount);
        Assert.Equal(CostBookingStatus.BookedManually, booking.Status);
    }

    [Fact]
    public void Invalid_amounts_or_second_booking_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CostBooking.CreatePending(
            DocumentId, CompanyId, "owner-1", Fingerprint(), 100m, 101m, IssueDate, Now));

        var booking = CostBooking.CreatePending(
            DocumentId, CompanyId, "owner-1", Fingerprint(), 100m, 23m, IssueDate, Now);
        booking.Book(
            null,
            "Koszt",
            0m,
            0m,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1),
            "nie ujmuj",
            false,
            Now);

        Assert.Throws<InvalidOperationException>(() => booking.Book(
            null,
            "Koszt",
            0m,
            0m,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 1),
            "ponownie",
            false,
            Now));
    }

    [Fact]
    public void Separate_ledgers_keep_explicit_zero_entries_as_not_included()
    {
        var bookingId = Guid.NewGuid();
        var month = new DateOnly(2026, 9, 1);

        var kpir = KpirEntry.Create(bookingId, DocumentId, "owner-1", month, "Koszt", 0m, null, Now);
        var vat = VatPurchaseEntry.Create(bookingId, DocumentId, "owner-1", month, 23m, 0m, null, Now);

        Assert.False(kpir.Included);
        Assert.False(vat.Included);
        Assert.Equal(23m, vat.InputVatAmount);
    }

    [Fact]
    public void Ledger_period_must_be_the_first_day_of_a_month()
    {
        Assert.Throws<ArgumentException>(() => KpirEntry.Create(
            Guid.NewGuid(), DocumentId, "owner-1", new DateOnly(2026, 9, 2), "Koszt", 10m, null, Now));
    }

    private static CostDocumentFingerprint Fingerprint()
        => CostDocumentFingerprint.Create(
            "PL123",
            "PL",
            "PLN",
            VatTreatment.DomesticTaxed,
            23m,
            CostServiceKind.Ai);
}
