namespace Firemka.Application.Security;

public interface ITrustedDeviceService
{
    Task<TrustedDeviceIssue> IssueAsync(
        string userId,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateAndTouchAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrustedDeviceSummary>> GetActiveAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default);
}
