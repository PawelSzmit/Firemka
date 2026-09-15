namespace Firemka.Domain.Notifications;

public enum NotificationKind
{
    Deadline = 1,
    InvoiceAction = 2,
    Error = 3,
    Backup = 4,
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
}

public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }
    public string OwnerUserId { get; private set; } = string.Empty;
    public NotificationKind Kind { get; private set; }
    public string IssueKey { get; private set; } = string.Empty;
    public DateOnly LocalDay { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string ActionPath { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }

    public static Notification Create(
        string ownerUserId,
        NotificationKind kind,
        string issueKey,
        DateOnly localDay,
        string subject,
        string body,
        string actionPath,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionPath);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!actionPath.StartsWith("/", StringComparison.Ordinal) || actionPath.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Odnośnik powiadomienia musi prowadzić wewnątrz Firemki.", nameof(actionPath));

        var normalizedIssue = issueKey.Trim();
        return new Notification
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId.Trim(),
            Kind = kind,
            IssueKey = normalizedIssue,
            LocalDay = localDay,
            DeduplicationKey = $"{normalizedIssue}:{localDay:yyyy-MM-dd}",
            Subject = subject.Trim(),
            Body = body.Trim(),
            ActionPath = actionPath.Trim(),
            Status = NotificationStatus.Pending,
            CreatedAtUtc = nowUtc,
        };
    }

    public void RecordFailure(string error, DateTimeOffset attemptedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        if (Status == NotificationStatus.Sent)
            throw new InvalidOperationException("Wysłane powiadomienie jest niezmienne.");
        AttemptCount++;
        LastAttemptAtUtc = attemptedAtUtc;
        LastError = error.Trim().Length <= 2_000 ? error.Trim() : error.Trim()[..2_000];
    }

    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        if (Status == NotificationStatus.Sent) return;
        AttemptCount++;
        LastAttemptAtUtc = sentAtUtc;
        SentAtUtc = sentAtUtc;
        LastError = null;
        Status = NotificationStatus.Sent;
    }
}
