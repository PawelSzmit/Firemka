using Firemka.Domain.Charging;

namespace Firemka.Domain.Tests;

public sealed class ChargingDomainTests
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Month = new(2026, 9, 1);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T08:00:00Z");

    [Fact]
    public void Profile_cannot_activate_until_wh_is_explicitly_confirmed()
    {
        var profile = CreateProfile();

        Assert.Throws<InvalidOperationException>(() => profile.Activate(whConfirmed: false, Now));

        profile.Activate(whConfirmed: true, Now);

        Assert.True(profile.IsActive);
        Assert.True(profile.WhConfirmed);
    }

    [Fact]
    public void Profile_rejects_ambiguous_columns_separator_and_timezone()
    {
        Assert.Throws<ArgumentException>(() => ChargingCsvProfile.Create(
            CompanyId, "owner-1", ';', "energy", "energy", "yyyy-MM-dd HH:mm", "Europe/Warsaw", null, Now));
        Assert.Throws<ArgumentException>(() => ChargingCsvProfile.Create(
            CompanyId, "owner-1", '"', "started", "energy", "yyyy-MM-dd HH:mm", "Europe/Warsaw", null, Now));
        Assert.Throws<TimeZoneNotFoundException>(() => ChargingCsvProfile.Create(
            CompanyId, "owner-1", ';', "started", "energy", "yyyy-MM-dd HH:mm", "Missing/Zone", null, Now));
    }

    [Fact]
    public void Profile_revision_is_pending_and_does_not_inherit_unit_confirmation()
    {
        var first = CreateProfile();
        first.Activate(true, Now);

        var second = first.CreateRevision(',', "time", "wh", "dd.MM.yyyy HH:mm", "Europe/Warsaw", "id", Now.AddDays(1));

        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(first.Id, second.PreviousProfileId);
        Assert.False(second.IsActive);
        Assert.False(second.WhConfirmed);
    }

    [Fact]
    public void Ten_thousand_wh_at_ninety_one_grosze_is_ten_kwh_and_nine_ten()
    {
        var session = Session(10_000m, "A");

        var report = ChargingReport.Create(
            CompanyId,
            "owner-1",
            Month,
            Guid.NewGuid(),
            0.91m,
            [session],
            versionNumber: 1,
            previousReportId: null,
            Now);

        Assert.Equal(10_000m, report.TotalWh);
        Assert.Equal(10m, report.TotalKwh);
        Assert.Equal(9.10m, report.GrossCost);
        Assert.Equal(ChargingReport.TaxBasisUnconfirmed, report.TaxStatus);
        Assert.Single(report.Sessions);
    }

    [Fact]
    public void Report_rejects_sessions_from_another_company_or_month()
    {
        var otherCompany = ChargingSession.Create(
            Guid.NewGuid(), "owner-1", Guid.NewGuid(), Month, Now, "2026-09-12 10:00", 1000m, Hash("A"), "A", Now);
        var otherMonth = ChargingSession.Create(
            CompanyId, "owner-1", Guid.NewGuid(), new DateOnly(2026, 10, 1), Now, "2026-10-01 10:00", 1000m, Hash("B"), "B", Now);

        Assert.Throws<InvalidOperationException>(() => CreateReport([otherCompany]));
        Assert.Throws<InvalidOperationException>(() => CreateReport([otherMonth]));
    }

    [Fact]
    public void Report_revision_points_to_history_and_does_not_change_previous_values()
    {
        var first = CreateReport([Session(1000m, "A")]);
        var second = ChargingReport.Create(
            CompanyId,
            "owner-1",
            Month,
            Guid.NewGuid(),
            1.10m,
            [Session(2000m, "B")],
            versionNumber: 2,
            previousReportId: first.Id,
            nowUtc: Now.AddDays(1));

        Assert.Equal(first.Id, second.PreviousReportId);
        Assert.Equal(0.91m, first.GrossRatePerKwh);
        Assert.Equal(0.91m, first.GrossCost);
        Assert.Equal(2.20m, second.GrossCost);
    }

    private static ChargingCsvProfile CreateProfile()
        => ChargingCsvProfile.Create(
            CompanyId,
            "owner-1",
            ';',
            "started",
            "energy",
            "yyyy-MM-dd HH:mm",
            "Europe/Warsaw",
            "session-id",
            Now);

    private static ChargingSession Session(decimal energyWh, string identity)
        => ChargingSession.Create(
            CompanyId,
            "owner-1",
            Guid.NewGuid(),
            Month,
            Now,
            "2026-09-12 10:00",
            energyWh,
            Hash(identity),
            identity,
            Now);

    private static ChargingReport CreateReport(IReadOnlyCollection<ChargingSession> sessions)
        => ChargingReport.Create(
            CompanyId,
            "owner-1",
            Month,
            Guid.NewGuid(),
            0.91m,
            sessions,
            1,
            null,
            Now);

    private static string Hash(string marker)
        => marker.PadRight(64, '0');
}
