namespace Firemka.Domain.Sales;

public sealed class SalesAutomationSettings
{
    private SalesAutomationSettings()
    {
    }

    private SalesAutomationSettings(
        Guid id,
        Guid companyId,
        string ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        Enabled = false;
        CreatedAtUtc = createdAtUtc;
        ChangedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }

    public static SalesAutomationSettings CreateDisabled(
        Guid companyId,
        string ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        return new SalesAutomationSettings(Guid.NewGuid(), companyId, ownerUserId.Trim(), createdAtUtc);
    }

    public void Enable(bool warningAcknowledged, DateTimeOffset changedAtUtc)
    {
        if (!warningAcknowledged)
        {
            throw new InvalidOperationException("Przed włączeniem automatyzacji trzeba potwierdzić ostrzeżenie.");
        }

        Enabled = true;
        ChangedAtUtc = changedAtUtc;
    }

    public void Disable(DateTimeOffset changedAtUtc)
    {
        Enabled = false;
        ChangedAtUtc = changedAtUtc;
    }
}
