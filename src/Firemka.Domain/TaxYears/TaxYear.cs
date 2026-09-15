namespace Firemka.Domain.TaxYears;

public sealed class TaxYear
{
    private TaxYear()
    {
    }

    private TaxYear(
        Guid id,
        Guid companyId,
        int year,
        TaxationForm taxationForm,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        Year = year;
        TaxationForm = taxationForm;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public int Year { get; private set; }

    public TaxationForm TaxationForm { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TaxYear Open(
        Guid companyId,
        int year,
        TaxationForm taxationForm,
        DateTimeOffset createdAtUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Id firmy nie może być pusty.", nameof(companyId));
        }

        if (year is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (taxationForm != TaxationForm.TaxScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxationForm),
                "Pierwsza wersja Firemki obsługuje wyłącznie skalę podatkową.");
        }

        return new TaxYear(Guid.NewGuid(), companyId, year, taxationForm, createdAtUtc);
    }
}
