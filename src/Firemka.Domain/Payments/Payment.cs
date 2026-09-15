namespace Firemka.Domain.Payments;

public enum PaymentKind
{
    ClientInvoice = 1,
    Pit = 2,
    Vat = 3,
    Zus = 4,
}

public enum PaymentSource
{
    Manual = 1,
    BankImport = 2,
}

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public PaymentKind Kind { get; private set; }
    public Guid TargetId { get; private set; }
    public int TargetVersion { get; private set; }
    public decimal AmountDue { get; private set; }
    public decimal AmountPaid { get; private set; }
    public DateOnly PaidOn { get; private set; }
    public PaymentSource Source { get; private set; }
    public string ExternalIdentifier { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Payment Record(
        Guid companyId,
        string ownerUserId,
        PaymentKind kind,
        Guid targetId,
        int targetVersion,
        decimal amountDue,
        decimal amountPaid,
        DateOnly paidOn,
        PaymentSource source,
        string externalIdentifier,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty || targetId == Guid.Empty)
            throw new ArgumentException("Firma i należność są wymagane.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalIdentifier);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(kind), "Rodzaj płatności jest nieprawidłowy.");
        if (targetVersion <= 0) throw new ArgumentOutOfRangeException(nameof(targetVersion));
        if (externalIdentifier.Trim().Length > 500)
            throw new ArgumentException("Referencja płatności może mieć maksymalnie 500 znaków.", nameof(externalIdentifier));

        var due = decimal.Round(amountDue, 2, MidpointRounding.AwayFromZero);
        var paid = decimal.Round(amountPaid, 2, MidpointRounding.AwayFromZero);
        if (due <= 0m) throw new ArgumentOutOfRangeException(nameof(amountDue));
        if (paid != due)
            throw new InvalidOperationException(
                "W pierwszej wersji można oznaczyć wyłącznie pełną kwotę należności.");

        return new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            Kind = kind,
            TargetId = targetId,
            TargetVersion = targetVersion,
            AmountDue = due,
            AmountPaid = paid,
            PaidOn = paidOn,
            Source = source,
            ExternalIdentifier = externalIdentifier.Trim(),
            CreatedAtUtc = nowUtc,
        };
    }
}
