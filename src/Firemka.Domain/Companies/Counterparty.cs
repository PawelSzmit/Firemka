namespace Firemka.Domain.Companies;

public sealed class Counterparty
{
    private Counterparty()
    {
    }

    internal Counterparty(Guid id, Guid companyId, string name, string nip, string address)
    {
        Id = id;
        CompanyId = companyId;
        Name = name;
        Nip = nip;
        Address = address;
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Nip { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;
}
