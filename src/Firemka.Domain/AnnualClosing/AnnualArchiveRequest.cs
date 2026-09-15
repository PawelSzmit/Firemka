namespace Firemka.Domain.AnnualClosing;

public sealed class AnnualArchiveRequest
{
    private AnnualArchiveRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public int TaxYear { get; private set; }
    public Guid AnnualClosingId { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static AnnualArchiveRequest Stage(
        Guid companyId, string ownerUserId, int taxYear, Guid annualClosingId, DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty || annualClosingId == Guid.Empty)
            throw new ArgumentException("Firma i zamknięcie roku są wymagane.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        return new AnnualArchiveRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            TaxYear = taxYear,
            AnnualClosingId = annualClosingId,
            RequestedAtUtc = nowUtc,
        };
    }

    public void Complete(DateTimeOffset nowUtc)
    {
        if (CompletedAtUtc is not null) throw new InvalidOperationException("Archiwum roczne jest już gotowe.");
        CompletedAtUtc = nowUtc;
    }
}
