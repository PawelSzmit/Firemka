using Firemka.Application.Time;

namespace Firemka.Application.Tests;

public sealed class PolishBusinessTimeTests
{
    [Fact]
    public void First_day_of_a_month_in_poland_is_not_taken_from_the_previous_utc_month()
    {
        var instant = DateTimeOffset.Parse("2026-09-30T22:30:00Z");

        var result = PolishBusinessTime.GetDate(instant);

        Assert.Equal(new DateOnly(2026, 10, 1), result);
    }

    [Fact]
    public void Display_time_is_always_converted_to_warsaw_even_if_the_server_uses_utc()
    {
        var instant = DateTimeOffset.Parse("2026-09-30T22:30:00Z");

        var result = PolishBusinessTime.ToWarsawTime(instant);

        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.FromHours(2)), result);
    }

    [Fact]
    public void Suggested_period_is_after_both_today_and_the_latest_saved_period()
    {
        var today = new DateOnly(2026, 9, 10);

        Assert.Equal(
            new DateOnly(2026, 10, 1),
            PolishBusinessTime.NextPeriodStart(today, new DateOnly(2026, 8, 1)));
        Assert.Equal(
            new DateOnly(2026, 11, 1),
            PolishBusinessTime.NextPeriodStart(today, new DateOnly(2026, 10, 1)));
        Assert.Equal(
            new DateOnly(2027, 2, 1),
            PolishBusinessTime.NextPeriodStart(today, new DateOnly(2027, 1, 1)));
    }
}
