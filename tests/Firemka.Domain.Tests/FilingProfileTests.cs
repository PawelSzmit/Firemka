using Firemka.Domain.Filings;

namespace Firemka.Domain.Tests;

public sealed class FilingProfileTests
{
    [Fact]
    public void Missing_identity_code_returns_a_controlled_validation_error()
    {
        var error = Assert.ThrowsAny<ArgumentException>(() => FilingProfileVersion.Create(
            Guid.NewGuid(), "owner-1", 1, null, "Jan", "Kowalski",
            new DateOnly(1990, 1, 1), null!, "0202", "0510", null,
            "synthetic:profile-check", new DateOnly(2026, 9, 13),
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"), new DateOnly(2026, 9, 13)));

        Assert.Contains("PESEL", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filing_profile_requires_person_identity_office_and_independent_confirmation()
    {
        var error = Assert.Throws<ArgumentException>(() => FilingProfileVersion.Create(
            Guid.NewGuid(),
            "owner-1",
            1,
            null,
            "Jan",
            "Kowalski",
            new DateOnly(1990, 1, 1),
            "123",
            "0202",
            "0510",
            "jan@example.test",
            "synthetic:profile-check",
            new DateOnly(2026, 9, 13),
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"),
            new DateOnly(2026, 9, 13)));

        Assert.Contains("PESEL", error.Message, StringComparison.OrdinalIgnoreCase);

        var profile = FilingProfileVersion.Create(
            Guid.NewGuid(),
            "owner-1",
            1,
            null,
            "Jan",
            "Kowalski",
            new DateOnly(1990, 1, 1),
            "90010112345",
            "0202",
            "0510",
            "jan@example.test",
            "synthetic:profile-check",
            new DateOnly(2026, 9, 13),
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"),
            new DateOnly(2026, 9, 13));

        Assert.Equal("90010112345", profile.Pesel);
        Assert.Equal("0202", profile.TaxOfficeCode);
        Assert.Equal("0510", profile.ZusInsuranceTitleCode);
        Assert.Equal(1, profile.VersionNumber);
    }

    [Theory]
    [InlineData("1899-12-31")]
    [InlineData("2026-09-14")]
    public void Filing_profile_rejects_an_impossible_birth_date(string birthDate)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => FilingProfileVersion.Create(
            Guid.NewGuid(), "owner-1", 1, null, "Jan", "Kowalski",
            DateOnly.Parse(birthDate), "90010112345", "0202", "0510", null,
            "synthetic:profile-check", new DateOnly(2026, 9, 13),
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"), new DateOnly(2026, 9, 13)));

        Assert.Contains("urodzenia", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2026-09-14")]
    public void Filing_profile_rejects_an_empty_or_future_confirmation_date(string confirmedOn)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => FilingProfileVersion.Create(
            Guid.NewGuid(), "owner-1", 1, null, "Jan", "Kowalski",
            new DateOnly(1990, 1, 1), "90010112345", "0202", "0510", null,
            "synthetic:profile-check", DateOnly.Parse(confirmedOn),
            DateTimeOffset.Parse("2026-09-13T22:30:00Z"), new DateOnly(2026, 9, 13)));

        Assert.Contains("potwierdzenia", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
