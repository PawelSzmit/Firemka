namespace Firemka.Domain.Invoices;

public sealed class InvoiceVersion
{
    private InvoiceVersion()
    {
    }

    private InvoiceVersion(
        Guid id,
        Guid invoiceId,
        int versionNumber,
        Guid? previousVersionId,
        string snapshotJson,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        InvoiceId = invoiceId;
        VersionNumber = versionNumber;
        PreviousVersionId = previousVersionId;
        SnapshotJson = snapshotJson;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public int VersionNumber { get; private set; }

    public Guid? PreviousVersionId { get; private set; }

    public string SnapshotJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static InvoiceVersion CreateInitial(
        Guid invoiceId,
        string snapshotJson,
        DateTimeOffset createdAtUtc)
    {
        return new InvoiceVersion(Guid.NewGuid(), invoiceId, 1, null, snapshotJson, createdAtUtc);
    }

    public InvoiceVersion CreateCorrection(string snapshotJson, DateTimeOffset createdAtUtc)
    {
        return new InvoiceVersion(Guid.NewGuid(), InvoiceId, VersionNumber + 1, Id, snapshotJson, createdAtUtc);
    }
}
