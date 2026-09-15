using Firemka.Domain.Payments;

namespace Firemka.Domain.Tests;

public sealed class PaymentTests
{
    [Fact]
    public void Full_payment_keeps_target_version_source_and_external_reference()
    {
        var payment = Payment.Record(
            Guid.NewGuid(), "owner-1", PaymentKind.Vat, Guid.NewGuid(), 2,
            1_234.56m, 1_234.56m, new DateOnly(2026, 10, 20),
            PaymentSource.Manual, "bank:transfer-123", DateTimeOffset.Parse("2026-10-20T08:00:00Z"));

        Assert.Equal(2, payment.TargetVersion);
        Assert.Equal(PaymentSource.Manual, payment.Source);
        Assert.Equal("bank:transfer-123", payment.ExternalIdentifier);
        Assert.Equal(1_234.56m, payment.AmountPaid);
    }

    [Fact]
    public void Partial_or_excess_payment_is_rejected()
    {
        var companyId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var partial = Assert.Throws<InvalidOperationException>(() => Payment.Record(
            companyId, "owner-1", PaymentKind.Pit, targetId, 1,
            1_000m, 999.99m, new DateOnly(2026, 10, 20),
            PaymentSource.Manual, "partial", DateTimeOffset.UtcNow));
        var excess = Assert.Throws<InvalidOperationException>(() => Payment.Record(
            companyId, "owner-1", PaymentKind.Pit, targetId, 1,
            1_000m, 1_000.01m, new DateOnly(2026, 10, 20),
            PaymentSource.Manual, "excess", DateTimeOffset.UtcNow));

        Assert.Contains("pełną kwotę", partial.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pełną kwotę", excess.Message, StringComparison.OrdinalIgnoreCase);
    }
}
