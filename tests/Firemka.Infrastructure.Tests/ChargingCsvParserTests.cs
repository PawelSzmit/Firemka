using System.Text;
using Firemka.Application.Charging;
using Firemka.Infrastructure.Charging;

namespace Firemka.Infrastructure.Tests;

public sealed class ChargingCsvParserTests
{
    private readonly ChargingCsvParser _parser = new();

    [Fact]
    public async Task Parses_quoted_fields_escaped_quotes_bom_and_warsaw_time()
    {
        var csv = "\uFEFFstarted;energy;session-id;note\r\n\"2026-09-01 01:30\";\"10000\";\"A;1\";\"tekst \"\"test\"\"\"\r\n";

        var result = await ParseAsync(csv);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(new DateOnly(2026, 9, 1), row.LocalMonth);
        Assert.Equal(DateTimeOffset.Parse("2026-08-31T23:30:00Z"), row.StartedAtUtc);
        Assert.Equal(10_000m, row.EnergyWh);
        Assert.Equal("A;1", row.SourceIdentity);
        Assert.Equal(64, row.NormalizedRowHash.Length);
        Assert.Equal(64, result.FileSha256.Length);
    }

    [Fact]
    public async Task One_invalid_row_returns_its_number_and_no_partial_result()
    {
        var error = await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync(
            "started;energy;session-id\n2026-09-01 01:00;1000;A\nwrong;2000;B"));

        Assert.Equal(3, error.RowNumber);
        Assert.Empty(error.AcceptedRows);
    }

    [Theory]
    [InlineData("started;energy;energy\n2026-09-01 01:00;1000;1000")]
    [InlineData("started;other;session-id\n2026-09-01 01:00;1000;A")]
    [InlineData("started;energy;session-id\n2026-09-01 01:00;0;A")]
    [InlineData("started;energy;session-id\n2026-09-01 01:00;-1;A")]
    [InlineData("started;energy;session-id\n\"2026-09-01 01:00;1000;A")]
    public async Task Invalid_headers_energy_or_quotes_are_rejected(string csv)
    {
        await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync(csv));
    }

    [Fact]
    public async Task Byte_and_row_limits_are_enforced_before_a_result_is_returned()
    {
        await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync(
            "started;energy;session-id\n2026-09-01 01:00;1000;A",
            new ChargingImportLimits(10, 10)));
        await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync(
            "started;energy;session-id\n2026-09-01 01:00;1000;A\n2026-09-01 02:00;1000;B",
            new ChargingImportLimits(10_000, 1)));
    }

    [Fact]
    public async Task Ambiguous_daylight_saving_time_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ChargingCsvValidationException>(() => ParseAsync(
            "started;energy;session-id\n2026-10-25 02:30;1000;A"));

        Assert.Contains("niejednoznacz", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parses_the_fixed_home_charger_export_with_extra_status_and_nfc_columns()
    {
        const string csv = "session_id,device_id,status,started_at,stopped_at,time [s],energy [Wh],power [W], tag NFC, tag NFC ID\n"
            + "synthetic-a,EVSE-SYNTHETIC,COMPLETED,2026-07-02 17:26:33,2026-07-02 17:34:14,461,64,498,undefined,undefined\n"
            + "synthetic-b,EVSE-SYNTHETIC,ABORTED,2026-07-25 06:58:22,2026-07-25 07:04:01,339,87,972,undefined,undefined";

        var result = await _parser.ParseAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            new ChargingProfileSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                ',',
                "started_at",
                "energy [Wh]",
                "yyyy-MM-dd HH:mm:ss",
                "Europe/Warsaw",
                "session_id",
                WhConfirmed: true,
                IsActive: true),
            ChargingImportLimits.Default);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(151m, result.Rows.Sum(item => item.EnergyWh));
        Assert.Equal("synthetic-b", result.Rows[1].SourceIdentity);
    }

    private Task<ChargingCsvParseResult> ParseAsync(
        string csv,
        ChargingImportLimits? limits = null)
        => _parser.ParseAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            new ChargingProfileSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                ';',
                "started",
                "energy",
                "yyyy-MM-dd HH:mm",
                "Europe/Warsaw",
                "session-id",
                WhConfirmed: true,
                IsActive: true),
            limits ?? ChargingImportLimits.Default);
}
