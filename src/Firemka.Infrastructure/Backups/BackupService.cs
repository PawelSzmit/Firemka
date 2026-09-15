using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Firemka.Application.Backups;
using Firemka.Domain.AnnualClosing;
using Firemka.Domain.Backups;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Firemka.Infrastructure.Backups;

public sealed class BackupService(AppDbContext dbContext) : IBackupService
{
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly TimeSpan MaximumAge = TimeSpan.FromHours(36);

    public async Task<BackupOverview> GetOverviewAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateOwner(ownerUserId);
        var token = await dbContext.BackupAccessTokens.AsNoTracking()
            .Where(item => item.OwnerUserId == owner && item.RevokedAtUtc == null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var state = await dbContext.BackupStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerUserId == owner, cancellationToken);
        var pendingManual = await dbContext.BackupRequests.AsNoTracking()
            .CountAsync(item => item.OwnerUserId == owner && item.CompletedAtUtc == null, cancellationToken);
        var annualYears = await dbContext.AnnualArchiveRequests.AsNoTracking()
            .Where(item => item.OwnerUserId == owner && item.CompletedAtUtc == null)
            .OrderBy(item => item.TaxYear)
            .Select(item => item.TaxYear)
            .ToListAsync(cancellationToken);

        return new BackupOverview(
            token is not null,
            token?.Prefix,
            state?.LastSuccessfulAtUtc,
            state?.LastAttemptAtUtc,
            state?.LastFileName,
            state?.LastFailureCode,
            state?.IsOverdue(nowUtc, MaximumAge, token is not null) ?? false,
            pendingManual,
            annualYears);
    }

    public async Task<IssuedBackupToken> IssueTokenAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateOwner(ownerUserId);
        await using var transaction = await AcquireOwnerWriteLockAsync(owner, cancellationToken);
        var active = await dbContext.BackupAccessTokens
            .Where(item => item.OwnerUserId == owner && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active) token.Revoke(nowUtc);

        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var secret = Base64Url(RandomNumberGenerator.GetBytes(32));
        var raw = $"fmbk_{prefix}_{secret}";
        dbContext.BackupAccessTokens.Add(BackupAccessToken.Create(owner, prefix, Sha256(raw), nowUtc));
        if (!await dbContext.BackupStates.AnyAsync(item => item.OwnerUserId == owner, cancellationToken))
            dbContext.BackupStates.Add(BackupState.Create(owner, nowUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new IssuedBackupToken(raw, prefix);
    }

    public async Task RevokeTokenAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateOwner(ownerUserId);
        await using var transaction = await AcquireOwnerWriteLockAsync(owner, cancellationToken);
        var active = await dbContext.BackupAccessTokens
            .Where(item => item.OwnerUserId == owner && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active) token.Revoke(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> RequestNowAsync(
        string ownerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var owner = ValidateOwner(ownerUserId);
        await using var transaction = await AcquireOwnerWriteLockAsync(owner, cancellationToken);
        var existing = await dbContext.BackupRequests.AsNoTracking()
            .Where(item => item.OwnerUserId == owner && item.CompletedAtUtc == null)
            .OrderBy(item => item.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return existing.Id;
        }
        var request = BackupRequest.CreateManual(owner, nowUtc);
        dbContext.BackupRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<BackupAuthorization?> AuthorizeAsync(
        string rawToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200) return null;
        var candidate = Convert.FromHexString(Sha256(rawToken.Trim()));
        var active = await dbContext.BackupAccessTokens
            .Where(item => item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        var token = active.FirstOrDefault(item => CryptographicOperations.FixedTimeEquals(
            candidate, Convert.FromHexString(item.SecretSha256)));
        if (token is null) return null;
        token.Touch(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BackupAuthorization(token.Id, token.OwnerUserId);
    }

    public async Task<BackupPlan> GetPlanAsync(
        BackupAuthorization authorization,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var state = await dbContext.BackupStates.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerUserId == authorization.OwnerUserId, cancellationToken);
        var manual = await dbContext.BackupRequests.AsNoTracking()
            .Where(item => item.OwnerUserId == authorization.OwnerUserId && item.CompletedAtUtc == null)
            .OrderBy(item => item.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var annual = await dbContext.AnnualArchiveRequests.AsNoTracking()
            .Where(item => item.OwnerUserId == authorization.OwnerUserId && item.CompletedAtUtc == null)
            .OrderBy(item => item.TaxYear)
            .Select(item => new AnnualBackupPlan(item.Id, item.TaxYear))
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, Warsaw).DateTime);
        var lastSuccessDay = state?.LastSuccessfulAtUtc is { } success
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(success, Warsaw).DateTime)
            : (DateOnly?)null;
        return new BackupPlan(manual is not null || lastSuccessDay != today, manual?.Id, annual);
    }

    public async Task ReportAsync(
        BackupAuthorization authorization,
        BackupReportCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Success && command.ManualRequestId is not null && command.AnnualRequestId is not null)
            throw new ArgumentException("Jeden raport może zakończyć tylko jeden rodzaj zlecenia kopii.", nameof(command));
        if (command.Success && command.FormatVersion != BackupPayloadWriter.CurrentFormatVersion)
            throw new ArgumentException("Raport dotyczy nieobsługiwanej wersji kopii.", nameof(command));

        await using var transaction = await AcquireOwnerWriteLockAsync(
            authorization.OwnerUserId, cancellationToken);
        BackupRequest? manual = null;
        AnnualArchiveRequest? annual = null;
        if (command.Success && command.ManualRequestId is { } manualId)
        {
            manual = await dbContext.BackupRequests.SingleOrDefaultAsync(
                item => item.Id == manualId && item.OwnerUserId == authorization.OwnerUserId,
                cancellationToken) ?? throw new KeyNotFoundException("Nie znaleziono zlecenia kopii.");
        }
        if (command.Success && command.AnnualRequestId is { } annualId)
        {
            annual = await dbContext.AnnualArchiveRequests.SingleOrDefaultAsync(
                item => item.Id == annualId && item.OwnerUserId == authorization.OwnerUserId,
                cancellationToken) ?? throw new KeyNotFoundException("Nie znaleziono zlecenia archiwum rocznego.");
        }
        if (command.Success)
            ValidateSuccessfulFileName(command.FileName, annual?.TaxYear);

        var state = await dbContext.BackupStates.SingleOrDefaultAsync(
            item => item.OwnerUserId == authorization.OwnerUserId, cancellationToken);
        if (state is null)
        {
            state = BackupState.Create(authorization.OwnerUserId, nowUtc);
            dbContext.BackupStates.Add(state);
        }

        if (command.Success)
        {
            state.RecordSuccess(
                command.FileName ?? string.Empty,
                command.ManifestSha256 ?? string.Empty,
                command.FormatVersion ?? 0,
                nowUtc);
            manual?.Complete(nowUtc);
            if (annual?.CompletedAtUtc is null) annual?.Complete(nowUtc);
        }
        else
        {
            state.RecordFailure(command.FailureCode ?? "client-failure", nowUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction?> AcquireOwnerWriteLockAsync(
        string ownerUserId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql()) return null;
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({ownerUserId}, 0))",
                cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static string ValidateOwner(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static void ValidateSuccessfulFileName(string? fileName, int? annualYear)
    {
        var pattern = annualYear is { } year
            ? $"^firemka-archive-{year}-[0-9]{{8}}T[0-9]{{9}}Z-v{BackupPayloadWriter.CurrentFormatVersion}\\.fmbak$"
            : $"^firemka-backup-[0-9]{{8}}T[0-9]{{9}}Z-v{BackupPayloadWriter.CurrentFormatVersion}\\.fmbak$";
        if (string.IsNullOrWhiteSpace(fileName)
            || !Regex.IsMatch(fileName, pattern, RegexOptions.CultureInvariant))
            throw new ArgumentException("Nazwa pliku nie odpowiada rodzajowi, rokowi albo wersji kopii.", nameof(fileName));
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
