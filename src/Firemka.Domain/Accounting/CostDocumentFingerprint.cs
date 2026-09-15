using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Firemka.Domain.Accounting;

public enum VatTreatment
{
    DomesticTaxed = 1,
    DomesticExempt = 2,
    ForeignService = 3,
    NoVat = 4,
}

public enum CostServiceKind
{
    Ai = 1,
    Vps = 2,
    Internet = 3,
    VehicleLeaseOrRental = 4,
    VehicleOperation = 5,
    VehicleInsurance = 6,
    PublicCharging = 7,
    Other = 8,
}

public enum AccountingPeriodPolicy
{
    IssueMonth = 1,
    NextMonth = 2,
    Manual = 3,
}

public sealed record CostDocumentFingerprint(
    string SellerKey,
    string SellerCountryCode,
    string Currency,
    VatTreatment VatTreatment,
    decimal? VatRate,
    CostServiceKind ServiceKind)
{
    public static CostDocumentFingerprint Create(
        string sellerKey,
        string sellerCountryCode,
        string currency,
        VatTreatment vatTreatment,
        decimal? vatRate,
        CostServiceKind serviceKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sellerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sellerCountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (!Enum.IsDefined(vatTreatment))
        {
            throw new ArgumentOutOfRangeException(nameof(vatTreatment));
        }

        if (!Enum.IsDefined(serviceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(serviceKind));
        }

        var normalizedSeller = new string(sellerKey
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
        if (normalizedSeller.Length == 0)
        {
            throw new ArgumentException("Identyfikator sprzedawcy nie może być pusty.", nameof(sellerKey));
        }

        var normalizedCountry = sellerCountryCode.Trim().ToUpperInvariant();
        if (normalizedCountry.Length != 2 || !normalizedCountry.All(char.IsLetter))
        {
            throw new ArgumentException("Kraj sprzedawcy musi być dwuliterowym kodem.", nameof(sellerCountryCode));
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsLetter))
        {
            throw new ArgumentException("Waluta musi być trzyliterowym kodem.", nameof(currency));
        }

        if (vatRate is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate), "Stawka VAT musi mieścić się od 0 do 100.");
        }

        if (vatTreatment == VatTreatment.DomesticTaxed && vatRate is null)
        {
            throw new ArgumentException("Dla opodatkowanego zakupu krajowego trzeba podać stawkę VAT.", nameof(vatRate));
        }

        if (vatTreatment != VatTreatment.DomesticTaxed)
        {
            vatRate = null;
        }

        return new CostDocumentFingerprint(
            normalizedSeller,
            normalizedCountry,
            normalizedCurrency,
            vatTreatment,
            vatRate,
            serviceKind);
    }

    public string ToKey()
    {
        var canonical = string.Join(
            '\u001F',
            SellerKey,
            SellerCountryCode,
            Currency,
            VatTreatment.ToString(),
            VatRate?.ToString("0.####", CultureInfo.InvariantCulture) ?? "-",
            ServiceKind.ToString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
