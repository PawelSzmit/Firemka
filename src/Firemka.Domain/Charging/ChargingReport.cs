using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Firemka.Domain.Charging;

public sealed class ChargingReport
{
    public const string TaxBasisUnconfirmed = "Tylko zestawienie — podstawa podatkowa niepotwierdzona";

    private readonly List<ChargingReportSession> _sessions = [];

    private ChargingReport()
    {
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public DateOnly Month { get; private set; }
    public Guid EnergyRatePeriodId { get; private set; }
    public decimal GrossRatePerKwh { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid? PreviousReportId { get; private set; }
    public decimal TotalWh { get; private set; }
    public decimal TotalKwh { get; private set; }
    public decimal GrossCost { get; private set; }
    public string TaxStatus { get; private set; } = TaxBasisUnconfirmed;
    public string InputFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ChargingReportSession> Sessions => _sessions.AsReadOnly();

    public static ChargingReport Create(
        Guid companyId,
        string ownerUserId,
        DateOnly month,
        Guid energyRatePeriodId,
        decimal grossRatePerKwh,
        IReadOnlyCollection<ChargingSession> sessions,
        int versionNumber,
        Guid? previousReportId,
        DateTimeOffset nowUtc)
    {
        if (companyId == Guid.Empty || energyRatePeriodId == Guid.Empty)
        {
            throw new ArgumentException("Identyfikatory raportu nie mogą być puste.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(sessions);
        if (month.Day != 1)
        {
            throw new ArgumentException("Raport musi dotyczyć pierwszego dnia miesiąca.", nameof(month));
        }

        if (grossRatePerKwh < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grossRatePerKwh));
        }

        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }

        if (sessions.Count == 0)
        {
            throw new InvalidOperationException("Nie można utworzyć pustego zestawienia ładowania.");
        }

        if (sessions.Any(item => item.CompanyId != companyId || item.OwnerUserId != ownerUserId || item.LocalMonth != month))
        {
            throw new InvalidOperationException("Wszystkie sesje muszą należeć do właściciela, firmy i miesiąca raportu.");
        }

        var exactKwh = sessions.Sum(item => item.EnergyWh) / 1000m;
        var report = new ChargingReport
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerUserId = ownerUserId.Trim(),
            Month = month,
            EnergyRatePeriodId = energyRatePeriodId,
            GrossRatePerKwh = grossRatePerKwh,
            VersionNumber = versionNumber,
            PreviousReportId = previousReportId,
            TotalWh = sessions.Sum(item => item.EnergyWh),
            TotalKwh = decimal.Round(exactKwh, 3, MidpointRounding.AwayFromZero),
            GrossCost = decimal.Round(exactKwh * grossRatePerKwh, 2, MidpointRounding.AwayFromZero),
            TaxStatus = TaxBasisUnconfirmed,
            CreatedAtUtc = nowUtc,
        };
        report.InputFingerprint = BuildFingerprint(energyRatePeriodId, grossRatePerKwh, sessions);
        report._sessions.AddRange(sessions.Select(item => ChargingReportSession.Create(report.Id, item.Id)));
        return report;
    }

    private static string BuildFingerprint(
        Guid rateId,
        decimal rate,
        IEnumerable<ChargingSession> sessions)
    {
        var canonical = string.Join(
            '\u001F',
            rateId.ToString("N"),
            rate.ToString("0.####", CultureInfo.InvariantCulture),
            string.Join(',', sessions.Select(item => item.Id.ToString("N")).Order(StringComparer.Ordinal)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed class ChargingReportSession
{
    private ChargingReportSession()
    {
    }

    public Guid ChargingReportId { get; private set; }
    public Guid ChargingSessionId { get; private set; }

    internal static ChargingReportSession Create(Guid reportId, Guid sessionId)
        => new()
        {
            ChargingReportId = reportId,
            ChargingSessionId = sessionId,
        };
}
