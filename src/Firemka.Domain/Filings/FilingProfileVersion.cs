namespace Firemka.Domain.Filings;

public sealed class FilingProfileVersion
{
    private FilingProfileVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public int VersionNumber { get; private set; }
    public Guid? PreviousVersionId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public DateOnly BirthDate { get; private set; }
    public string Pesel { get; private set; } = string.Empty;
    public string TaxOfficeCode { get; private set; } = string.Empty;
    public string ZusInsuranceTitleCode { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string ConfirmationEvidence { get; private set; } = string.Empty;
    public DateOnly ConfirmedOn { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FilingProfileVersion Create(
        Guid companyId,
        string ownerUserId,
        int versionNumber,
        Guid? previousVersionId,
        string firstName,
        string lastName,
        DateOnly birthDate,
        string pesel,
        string taxOfficeCode,
        string zusInsuranceTitleCode,
        string? email,
        string confirmationEvidence,
        DateOnly confirmedOn,
        DateTimeOffset createdAtUtc,
        DateOnly currentBusinessDate)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pesel);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxOfficeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(zusInsuranceTitleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationEvidence);
        if (firstName.Trim().Length > 100 || lastName.Trim().Length > 150)
        {
            throw new ArgumentException("Imię lub nazwisko jest zbyt długie.");
        }

        if (confirmationEvidence.Trim().Length > 2_000)
        {
            throw new ArgumentException("Opis sprawdzenia może mieć maksymalnie 2000 znaków.", nameof(confirmationEvidence));
        }
        if (birthDate < new DateOnly(1900, 1, 1) || birthDate >= currentBusinessDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(birthDate),
                "Data urodzenia musi przypadać między 01.01.1900 a dniem poprzedzającym zapis profilu.");
        }
        if (confirmedOn == default || confirmedOn > currentBusinessDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedOn),
                "Data potwierdzenia jest wymagana i nie może przypadać w przyszłości.");
        }
        if (pesel.Length != 11 || !pesel.All(char.IsDigit))
        {
            throw new ArgumentException("PESEL musi mieć 11 cyfr.", nameof(pesel));
        }

        ValidateCode(taxOfficeCode, 4, "Kod urzędu skarbowego", nameof(taxOfficeCode));
        ValidateCode(zusInsuranceTitleCode, 4, "Kod tytułu ubezpieczenia ZUS", nameof(zusInsuranceTitleCode));
        if (versionNumber <= 0
            || (versionNumber == 1 && previousVersionId is not null)
            || (versionNumber > 1 && previousVersionId is null))
        {
            throw new ArgumentException("Łańcuch wersji profilu jest nieprawidłowy.");
        }

        if (!string.IsNullOrWhiteSpace(email)
            && (!email.Contains('@', StringComparison.Ordinal) || email.Trim().Length > 254))
        {
            throw new ArgumentException("Adres e-mail jest nieprawidłowy.", nameof(email));
        }

        return new FilingProfileVersion
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            VersionNumber = versionNumber,
            PreviousVersionId = previousVersionId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            BirthDate = birthDate,
            Pesel = pesel,
            TaxOfficeCode = taxOfficeCode,
            ZusInsuranceTitleCode = zusInsuranceTitleCode,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            ConfirmationEvidence = confirmationEvidence.Trim(),
            ConfirmedOn = confirmedOn,
            CreatedAtUtc = createdAtUtc,
        };
    }

    private static void ValidateCode(string value, int length, string label, string parameterName)
    {
        if (value.Length != length || !value.All(char.IsDigit))
        {
            throw new ArgumentException($"{label} musi mieć {length} cyfry.", parameterName);
        }
    }
}
