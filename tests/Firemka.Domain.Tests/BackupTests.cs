using Firemka.Domain.Backups;

namespace Firemka.Domain.Tests;

public sealed class BackupTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T10:00:00Z");

    [Fact]
    public void Token_can_be_touched_and_revoked_but_never_contains_the_raw_secret()
    {
        var hash = new string('A', 64);
        var token = BackupAccessToken.Create("owner-1", "ABCD1234", hash, Now);

        Assert.Equal(hash, token.SecretSha256);
        token.Touch(Now.AddMinutes(1));
        token.Revoke(Now.AddMinutes(2));

        Assert.False(token.IsActive);
        Assert.Equal(Now.AddMinutes(1), token.LastUsedAtUtc);
        Assert.Throws<InvalidOperationException>(() => token.Touch(Now.AddMinutes(3)));
    }

    [Fact]
    public void Backup_state_is_overdue_only_after_configuration_and_records_verified_result()
    {
        var state = BackupState.Create("owner-1", Now);
        Assert.False(state.IsOverdue(Now.AddDays(10), TimeSpan.FromHours(36), hasActiveToken: false));
        Assert.True(state.IsOverdue(Now.AddHours(37), TimeSpan.FromHours(36), hasActiveToken: true));

        state.RecordSuccess("firemka-backup.fmbak", new string('B', 64), 1, Now.AddHours(1));
        Assert.False(state.IsOverdue(Now.AddHours(36), TimeSpan.FromHours(36), hasActiveToken: true));
        Assert.Throws<ArgumentException>(() => state.RecordSuccess("../escape.fmbak", new string('B', 64), 1, Now));
    }

    [Fact]
    public void Manual_request_is_idempotently_completed()
    {
        var request = BackupRequest.CreateManual("owner-1", Now);
        request.Complete(Now.AddMinutes(1));
        request.Complete(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), request.CompletedAtUtc);
    }
}
