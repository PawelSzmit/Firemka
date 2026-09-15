namespace Firemka.Domain.AnnualClosing;

public sealed class AnnualDeclaration
{
    private AnnualDeclaration()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public int TaxYear { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousDeclarationId { get; private set; }
    public decimal OpeningInventory { get; private set; }
    public decimal ClosingInventory { get; private set; }
    public decimal PitAdvancesPaid { get; private set; }
    public decimal HealthContributionsPaid { get; private set; }
    public bool IndependentVerificationConfirmed { get; private set; }
    public string EvidenceReference { get; private set; } = string.Empty;
    public DateOnly ConfirmedOn { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static AnnualDeclaration Create(
        Guid companyId,
        string ownerUserId,
        int taxYear,
        int versionNumber,
        Guid? previousDeclarationId,
        decimal openingInventory,
        decimal closingInventory,
        decimal pitAdvancesPaid,
        decimal healthContributionsPaid,
        bool independentVerificationConfirmed,
        string evidenceReference,
        DateOnly confirmedOn,
        DateTimeOffset createdAtUtc,
        DateOnly currentBusinessDate)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("Firma jest wymagana.", nameof(companyId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        if (taxYear is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(taxYear));
        }
        if (versionNumber <= 0
            || (versionNumber == 1 && previousDeclarationId is not null)
            || (versionNumber > 1 && previousDeclarationId is null))
        {
            throw new ArgumentException("Łańcuch wersji deklaracji rocznej jest nieprawidłowy.");
        }
        if (openingInventory < 0m || closingInventory < 0m
            || pitAdvancesPaid < 0m || healthContributionsPaid < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(openingInventory), "Kwoty roczne nie mogą być ujemne.");
        }
        if (!independentVerificationConfirmed || string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new ArgumentException(
                "Przed zamknięciem roku potwierdź niezależne sprawdzenie i wskaż dowód.",
                nameof(independentVerificationConfirmed));
        }
        if (evidenceReference.Trim().Length > 2_000)
        {
            throw new ArgumentException("Opis dowodu może mieć maksymalnie 2000 znaków.", nameof(evidenceReference));
        }
        if (confirmedOn == default || confirmedOn > currentBusinessDate)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmedOn), "Data potwierdzenia jest nieprawidłowa.");
        }

        return new AnnualDeclaration
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            TaxYear = taxYear,
            VersionNumber = versionNumber,
            PreviousDeclarationId = previousDeclarationId,
            OpeningInventory = decimal.Round(openingInventory, 2),
            ClosingInventory = decimal.Round(closingInventory, 2),
            PitAdvancesPaid = decimal.Round(pitAdvancesPaid, 2),
            HealthContributionsPaid = decimal.Round(healthContributionsPaid, 2),
            IndependentVerificationConfirmed = true,
            EvidenceReference = evidenceReference.Trim(),
            ConfirmedOn = confirmedOn,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
