namespace Firemka.Application.Time;

public static class PolishBusinessTime
{
    private static readonly TimeZoneInfo WarsawTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static DateOnly GetDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(ToWarsawTime(instant).DateTime);

    public static DateTimeOffset ToWarsawTime(DateTimeOffset instant)
        => TimeZoneInfo.ConvertTime(instant, WarsawTimeZone);

    public static DateOnly Today(TimeProvider timeProvider)
        => GetDate(timeProvider.GetUtcNow());

    public static DateOnly NextPeriodStart(DateOnly today, DateOnly latestPeriodStart)
    {
        var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        var nextCalendarMonth = firstOfThisMonth.AddMonths(1);
        var monthAfterLatestPeriod = new DateOnly(
            latestPeriodStart.Year,
            latestPeriodStart.Month,
            1).AddMonths(1);
        return nextCalendarMonth >= monthAfterLatestPeriod
            ? nextCalendarMonth
            : monthAfterLatestPeriod;
    }
}
