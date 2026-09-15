namespace Firemka.Domain.Accounting;

public sealed class VatPurchaseEntry
{
    private VatPurchaseEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateOnly Period { get; private set; }
    public decimal InputVatAmount { get; private set; }
    public decimal DeductibleVatAmount { get; private set; }
    public bool Included { get; private set; }
    public Guid? RuleId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VatPurchaseEntry Create(
        Guid bookingId,
        Guid sourceDocumentId,
        string ownerUserId,
        DateOnly period,
        decimal inputVatAmount,
        decimal deductibleVatAmount,
        Guid? ruleId,
        DateTimeOffset nowUtc)
    {
        if (bookingId == Guid.Empty || sourceDocumentId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikatory wpisu nie mogą być puste.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (period.Day != 1)
        {
            throw new ArgumentException("Okres musi zaczynać się pierwszego dnia miesiąca.", nameof(period));
        }

        if (inputVatAmount < 0 || deductibleVatAmount < 0 || deductibleVatAmount > inputVatAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(deductibleVatAmount), "VAT do odliczenia nie może przekraczać VAT naliczonego.");
        }

        return new VatPurchaseEntry
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SourceDocumentId = sourceDocumentId,
            OwnerUserId = ownerUserId.Trim(),
            Period = period,
            InputVatAmount = inputVatAmount,
            DeductibleVatAmount = deductibleVatAmount,
            Included = deductibleVatAmount > 0,
            RuleId = ruleId,
            CreatedAtUtc = nowUtc,
        };
    }
}
