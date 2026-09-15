using System.Security.Cryptography;
using System.Text;
using Firemka.Application.Security;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Security;

public sealed class TrustedDeviceService(AppDbContext dbContext) : ITrustedDeviceService
{
    private const int MaximumActiveDevices = 10;
    private static readonly TimeSpan TrustLifetime = TimeSpan.FromDays(30);

    public async Task<TrustedDeviceIssue> IssueAsync(
        string userId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = DateTimeOffset.UtcNow;
        var token = CreateToken();
        var expiresAtUtc = now.Add(TrustLifetime);

        var excessDevices = await dbContext.TrustedDevices
            .Where(device => device.UserId == userId && device.RevokedAtUtc == null && device.ExpiresAtUtc > now)
            .OrderBy(device => device.LastUsedAtUtc)
            .Skip(MaximumActiveDevices - 1)
            .ToListAsync(cancellationToken);

        foreach (var device in excessDevices)
        {
            device.RevokedAtUtc = now;
        }

        dbContext.TrustedDevices.Add(new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            DisplayName = TruncateDisplayName(displayName),
            CreatedAtUtc = now,
            LastUsedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TrustedDeviceIssue(token, expiresAtUtc);
    }

    public async Task<bool> ValidateAndTouchAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(token);
        var device = await dbContext.TrustedDevices.SingleOrDefaultAsync(
            item => item.UserId == userId
                && item.TokenHash == tokenHash
                && item.RevokedAtUtc == null
                && item.ExpiresAtUtc > now,
            cancellationToken);

        if (device is null)
        {
            return false;
        }

        device.LastUsedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TrustedDeviceSummary>> GetActiveAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.TrustedDevices
            .AsNoTracking()
            .Where(device => device.UserId == userId && device.RevokedAtUtc == null && device.ExpiresAtUtc > now)
            .OrderByDescending(device => device.LastUsedAtUtc)
            .Select(device => new TrustedDeviceSummary(
                device.Id,
                device.DisplayName,
                device.CreatedAtUtc,
                device.LastUsedAtUtc,
                device.ExpiresAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = DateTimeOffset.UtcNow;
        var activeDevices = await dbContext.TrustedDevices
            .Where(device => device.UserId == userId && device.RevokedAtUtc == null && device.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var device in activeDevices)
        {
            device.RevokedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CreateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string TruncateDisplayName(string displayName)
    {
        const int maximumLength = 120;
        var cleanedName = string.IsNullOrWhiteSpace(displayName) ? "Nieznane urządzenie" : displayName.Trim();
        return cleanedName.Length <= maximumLength ? cleanedName : cleanedName[..maximumLength];
    }
}
