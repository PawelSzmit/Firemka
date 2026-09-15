using Firemka.Domain.Notifications;

namespace Firemka.Domain.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void Notification_has_a_stable_daily_key_and_records_delivery_failure_before_success()
    {
        var notification = Notification.Create(
            "owner-1", NotificationKind.Deadline, "vat:settlement-1", new DateOnly(2026, 10, 18),
            "Termin VAT", "Sprawdź płatność w Firemce.", "/Payments?year=2026&month=9",
            DateTimeOffset.Parse("2026-10-18T06:00:00Z"));

        Assert.Equal("vat:settlement-1:2026-10-18", notification.DeduplicationKey);
        notification.RecordFailure("SMTP niedostępny", DateTimeOffset.Parse("2026-10-18T06:01:00Z"));
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(1, notification.AttemptCount);
        notification.MarkSent(DateTimeOffset.Parse("2026-10-18T06:02:00Z"));
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(2, notification.AttemptCount);
    }
}
