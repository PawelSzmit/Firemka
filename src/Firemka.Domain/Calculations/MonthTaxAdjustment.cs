using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Firemka.Domain.Calculations;

public enum MonthAdjustmentKind
{
    PitRevenue = 1,
    KpirCost = 2,
    VatOutput = 3,
    VatInput = 4,
    HealthIncome = 5,
    ForeignServiceVatOutput = 6,
    SalesRecognition = 7,
}

public sealed class MonthTaxAdjustment
{
    private MonthTaxAdjustment()
    {
    }

    private MonthTaxAdjustment(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        MonthAdjustmentKind kind,
        decimal amount,
        string reason,
        string evidenceReference,
        Guid? sourceDocumentId,
        Guid? salesInvoiceId,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        OwnerUserId = ownerUserId;
        Month = month;
        Kind = kind;
        Amount = amount;
        Reason = reason;
        EvidenceReference = evidenceReference;
        SourceDocumentId = sourceDocumentId;
        SalesInvoiceId = salesInvoiceId;
        CreatedAtUtc = createdAtUtc;
        ValueFingerprint = CreateFingerprint(
            kind,
            amount,
            reason,
            evidenceReference,
            sourceDocumentId,
            salesInvoiceId);
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string OwnerUserId { get; private set; } = string.Empty;

    public DateOnly Month { get; private set; }

    public MonthAdjustmentKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EvidenceReference { get; private set; } = string.Empty;

    public Guid? SourceDocumentId { get; private set; }

    public Guid? SalesInvoiceId { get; private set; }

    public string ValueFingerprint { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static MonthTaxAdjustment Create(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        MonthAdjustmentKind kind,
        decimal amount,
        string reason,
        string evidenceReference,
        Guid? sourceDocumentId,
        Guid? salesInvoiceId,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);
        if (month.Day != 1)
        {
            throw new ArgumentException("Miesiąc korekty musi zaczynać się pierwszego dnia.", nameof(month));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Nieobsługiwany rodzaj korekty.");
        }

        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (kind == MonthAdjustmentKind.SalesRecognition && roundedAmount != 0m)
        {
            throw new InvalidOperationException(
                "Potwierdzenie okresu sprzedaży nie może zmieniać kwoty rozliczenia.");
        }

        if (kind != MonthAdjustmentKind.SalesRecognition && roundedAmount == 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Kwota korekty nie może wynosić zero.");
        }

        ValidateLink(sourceDocumentId, nameof(sourceDocumentId));
        ValidateLink(salesInvoiceId, nameof(salesInvoiceId));
        if (sourceDocumentId is not null && salesInvoiceId is not null)
        {
            throw new ArgumentException("Korekta może wskazywać najwyżej jedno źródło.");
        }

        if (kind == MonthAdjustmentKind.ForeignServiceVatOutput && sourceDocumentId is null)
        {
            throw new InvalidOperationException(
                "Korekta VAT importu usług wymaga powiązanego dokumentu źródłowego.");
        }

        if (kind == MonthAdjustmentKind.SalesRecognition
            && (salesInvoiceId is null || sourceDocumentId is not null))
        {
            throw new InvalidOperationException(
                "Potwierdzenie okresu sprzedaży wymaga jednej powiązanej faktury sprzedaży.");
        }

        return new MonthTaxAdjustment(
            companyId,
            ownerUserId.Trim(),
            month,
            kind,
            roundedAmount,
            reason.Trim(),
            evidenceReference.Trim(),
            sourceDocumentId,
            salesInvoiceId,
            nowUtc);
    }

    private static void ValidateLink(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identyfikator źródła nie może być pusty.", parameterName);
        }
    }

    private static string CreateFingerprint(
        MonthAdjustmentKind kind,
        decimal amount,
        string reason,
        string evidenceReference,
        Guid? sourceDocumentId,
        Guid? salesInvoiceId)
    {
        var canonical = string.Join(
            '|',
            kind.ToString(),
            amount.ToString("G29", CultureInfo.InvariantCulture),
            reason,
            evidenceReference,
            sourceDocumentId?.ToString("N") ?? "null",
            salesInvoiceId?.ToString("N") ?? "null");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
