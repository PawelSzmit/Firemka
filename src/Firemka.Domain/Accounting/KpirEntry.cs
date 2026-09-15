namespace Firemka.Domain.Accounting;

public sealed class KpirEntry
{
    private KpirEntry()
    {
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateOnly Period { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool Included { get; private set; }
    public Guid? RuleId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static KpirEntry Create(
        Guid bookingId,
        Guid sourceDocumentId,
        string ownerUserId,
        DateOnly period,
        string category,
        decimal amount,
        Guid? ruleId,
        DateTimeOffset nowUtc)
    {
        Validate(bookingId, sourceDocumentId, ownerUserId, period, amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return new KpirEntry
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SourceDocumentId = sourceDocumentId,
            OwnerUserId = ownerUserId.Trim(),
            Period = period,
            Category = category.Trim(),
            Amount = amount,
            Included = amount > 0,
            RuleId = ruleId,
            CreatedAtUtc = nowUtc,
        };
    }

    private static void Validate(Guid bookingId, Guid sourceDocumentId, string ownerUserId, DateOnly period, decimal amount)
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

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }
}
