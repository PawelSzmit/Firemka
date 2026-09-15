using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Filings;
using AnnualClosingEntity = Firemka.Domain.AnnualClosing.AnnualClosing;

namespace Firemka.Domain.Tests;

public sealed class AnnualClosingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2027-01-10T10:00:00Z");

    [Fact]
    public void Declaration_requires_independent_confirmation_and_valid_amounts()
    {
        var error = Assert.Throws<ArgumentException>(() => AnnualDeclaration.Create(
            Guid.NewGuid(), "owner", 2026, 1, null,
            0m, 0m, 1200m, 1000m, false, "", new DateOnly(2027, 1, 10), Now, new DateOnly(2027, 1, 10)));

        Assert.Contains("niezależne sprawdzenie", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ArgumentOutOfRangeException>(() => AnnualDeclaration.Create(
            Guid.NewGuid(), "owner", 2026, 1, null,
            -1m, 0m, 1200m, 1000m, true, "księgowa", new DateOnly(2027, 1, 10), Now, new DateOnly(2027, 1, 10)));
    }

    [Fact]
    public void Closing_is_immutable_and_correction_preserves_the_chain()
    {
        var companyId = Guid.NewGuid();
        var declarationId = Guid.NewGuid();
        var months = Enumerable.Range(1, 12)
            .Select(month => new AnnualClosingMonthInput(
                new DateOnly(2026, month, 1), Guid.NewGuid(), Guid.NewGuid(), month * 100m, month * 10m,
                month, month * 2m, month * 3m))
            .ToArray();
        var values = new AnnualClosingValues(
            Revenue: 12000m,
            CostsBeforeInventory: 2000m,
            OpeningInventory: 100m,
            ClosingInventory: 250m,
            CostsAfterInventory: 1850m,
            SocialContributions: 0m,
            PitAdjustments: 0m,
            PitIncome: 10150m,
            PitAdvancesDue: 900m,
            PitAdvancesPaid: 800m,
            HealthIncome: 10000m,
            AnnualHealthMinimumBase: 57672m,
            AnnualHealthBasis: 57672m,
            AnnualHealthContributionDue: 5190.48m,
            HealthContributionsDueMonthly: 5000m,
            HealthContributionsPaid: 5000m,
            HealthSettlementDifference: 190.48m,
            HealthPaymentDifference: 190.48m);

        var first = AnnualClosingEntity.CloseOriginal(
            companyId, "owner", 2026, declarationId, values, months,
            Guid.NewGuid(), new string('A', 64), "jpk-r1",
            Guid.NewGuid(), new string('B', 64), "pdf-r1",
            new string('C', 64), true, Now);

        Assert.Equal(AnnualClosingStatus.Closed, first.Status);
        Assert.Equal(FilingArtifactStatus.ApprovalRequired, first.JpkStatus);
        Assert.Equal(12, first.Months.Count);
        Assert.Equal(1, first.VersionNumber);

        var correction = AnnualClosingEntity.StartCorrection(first, "Korekta kosztu", Now.AddDays(1));
        Assert.Equal(AnnualClosingStatus.OpenCorrection, correction.Status);
        Assert.Equal(first.Id, correction.PreviousClosingId);
        Assert.Equal(2, correction.VersionNumber);
        Assert.Empty(correction.Months);

        correction.Close(
            declarationId, values with { CostsBeforeInventory = 2100m, CostsAfterInventory = 1950m, PitIncome = 10050m },
            months, Guid.NewGuid(), new string('D', 64), "jpk-r1",
            Guid.NewGuid(), new string('E', 64), "pdf-r1", new string('F', 64), true, Now.AddDays(2));

        Assert.Equal(AnnualClosingStatus.Closed, correction.Status);
        Assert.Equal(first.Id, correction.PreviousClosingId);
        Assert.Equal(2100m, correction.Values.CostsBeforeInventory);
    }

    [Fact]
    public void Annual_jpk_requires_approval_then_manual_send_before_recording_outcome()
    {
        var closing = CreateClosedYear();

        Assert.Throws<InvalidOperationException>(() =>
            closing.MarkJpkSent("Klient JPK WEB", Now));
        closing.ApproveJpk("Porównano P_1-P_4 z zamknięciem roku", Now);
        closing.MarkJpkSent("Klient JPK WEB, numer referencyjny 123", Now.AddMinutes(1));
        closing.RecordJpkOutcome(
            FilingSubmissionOutcome.Accepted, Guid.NewGuid(), "UPO 123", Now.AddMinutes(2));

        Assert.Equal(FilingArtifactStatus.Accepted, closing.JpkStatus);
        Assert.Equal("UPO 123", closing.JpkOutcomeReference);
        Assert.NotNull(closing.JpkReceiptStoredFileId);
    }

    private static AnnualClosingEntity CreateClosedYear()
    {
        AnnualClosingMonthInput[] months = [new AnnualClosingMonthInput(
            new DateOnly(2026, 12, 1), Guid.NewGuid(), Guid.NewGuid(), 100m, 10m, 5m, 90m, 9m)];
        var values = new AnnualClosingValues(
            100m, 10m, 0m, 0m, 10m, 0m, 0m, 90m, 5m, 5m,
            90m, 90m, 90m, 9m, 9m, 9m, 0m, 0m);
        return AnnualClosingEntity.CloseOriginal(
            Guid.NewGuid(), "owner", 2026, Guid.NewGuid(), values, months,
            Guid.NewGuid(), new string('A', 64), "jpk-r1",
            Guid.NewGuid(), new string('B', 64), "pdf-r1",
            new string('C', 64), true, Now);
    }
}
