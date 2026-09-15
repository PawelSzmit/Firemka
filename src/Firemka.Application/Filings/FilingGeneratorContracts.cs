namespace Firemka.Application.Filings;

public sealed record FilingPersonIdentity(
    string Nip,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Pesel,
    string TaxOfficeCode,
    string ZusInsuranceTitleCode,
    string? Email);

public sealed record JpkVatSalesRow(
    string CounterpartyTaxId,
    string CounterpartyName,
    string EvidenceNumber,
    DateOnly IssueDate,
    DateOnly SaleDate,
    string? KsefNumber,
    decimal NetAmount23,
    decimal VatAmount23);

public sealed record JpkVatPurchaseRow(
    string? SupplierCountryCode,
    string SupplierTaxId,
    string SupplierName,
    string EvidenceNumber,
    DateOnly PurchaseDate,
    DateOnly ReceivedDate,
    string? KsefNumber,
    decimal NetOtherAmount,
    decimal DeductibleVatAmount);

public sealed record JpkV7M3Input(
    FilingPersonIdentity Identity,
    DateOnly Month,
    DateTimeOffset GeneratedAtUtc,
    int SubmissionPurpose,
    IReadOnlyList<JpkVatSalesRow> Sales,
    IReadOnlyList<JpkVatPurchaseRow> Purchases,
    decimal PriorVatCarryForward);

public interface IJpkV7M3Generator
{
    string Generate(JpkV7M3Input input);

    void Validate(string xml);
}

public sealed record JpkPkpirRow(
    DateOnly EventDate,
    string EvidenceNumber,
    string? KsefNumber,
    string? CounterpartyCountryCode,
    string? CounterpartyTaxId,
    string CounterpartyName,
    string CounterpartyAddress,
    string Description,
    decimal Revenue,
    decimal OtherExpense);

public sealed record JpkPkpir3Input(
    FilingPersonIdentity Identity,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    DateTimeOffset GeneratedAtUtc,
    int SubmissionPurpose,
    IReadOnlyList<JpkPkpirRow> Rows,
    decimal OpeningInventory = 0m,
    decimal ClosingInventory = 0m);

public interface IJpkPkpir3Generator
{
    string Generate(JpkPkpir3Input input);
    void Validate(string xml);
}

public sealed record ZusDraKedu227Input(
    FilingPersonIdentity Identity,
    DateOnly Month,
    DateOnly PreparedOn,
    int FilingSequenceNumber,
    decimal HealthBasis,
    decimal HealthContribution);

public interface IZusDraKedu227Generator
{
    string Generate(ZusDraKedu227Input input);
    void Validate(string xml);
}
