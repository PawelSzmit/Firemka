using Firemka.Application.Charging;
using Firemka.Domain.Charging;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Charging;

public sealed class HomeChargingService(
    AppDbContext dbContext,
    IChargingCsvParser parser) : IHomeChargingService
{
    public async Task<ChargingProfileSnapshot> SaveProfileAsync(
        string ownerUserId,
        Guid companyId,
        SaveChargingProfileCommand command,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(command);
        await EnsureOwnedCompanyAsync(ownerUserId, companyId, cancellationToken);
        var latest = await dbContext.ChargingCsvProfiles
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && SameConfiguration(latest, command))
        {
            return ToSnapshot(latest);
        }

        var profile = latest is null
            ? ChargingCsvProfile.Create(
                companyId,
                ownerUserId,
                command.Separator,
                command.TimestampColumn,
                command.EnergyWhColumn,
                command.DateFormat,
                command.TimeZoneId,
                command.IdentityColumn,
                nowUtc)
            : latest.CreateRevision(
                command.Separator,
                command.TimestampColumn,
                command.EnergyWhColumn,
                command.DateFormat,
                command.TimeZoneId,
                command.IdentityColumn,
                nowUtc);
        dbContext.ChargingCsvProfiles.Add(profile);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(profile);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.ChargingCsvProfiles.AsNoTracking()
                .Where(item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (replay is not null && SameConfiguration(replay, command))
            {
                return ToSnapshot(replay);
            }

            throw new InvalidOperationException(
                "W tym samym czasie zapisano inną wersję profilu CSV. Odśwież dane i spróbuj ponownie.",
                exception);
        }
    }

    public async Task<ChargingProfileSnapshot> ActivateProfileAsync(
        string ownerUserId,
        Guid profileId,
        bool whConfirmed,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var profile = await dbContext.ChargingCsvProfiles.SingleOrDefaultAsync(
            item => item.Id == profileId && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono profilu CSV.");
        if (profile.IsActive)
        {
            return ToSnapshot(profile);
        }

        var activeProfiles = await dbContext.ChargingCsvProfiles
            .Where(item => item.CompanyId == profile.CompanyId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var active in activeProfiles)
        {
            active.Deactivate(nowUtc);
        }

        profile.Activate(whConfirmed, nowUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToSnapshot(profile);
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.ChargingCsvProfiles.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == profileId && item.OwnerUserId == ownerUserId,
                cancellationToken);
            if (replay?.IsActive == true)
            {
                return ToSnapshot(replay);
            }

            throw new InvalidOperationException(
                "W tym samym czasie aktywowano inną wersję profilu CSV. Odśwież dane przed ponowną próbą.",
                exception);
        }
    }

    public async Task<ChargingImportSnapshot> ImportAsync(
        string ownerUserId,
        Guid companyId,
        Guid profileId,
        Stream csv,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnedCompanyAsync(ownerUserId, companyId, cancellationToken);
        var profile = await dbContext.ChargingCsvProfiles.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == profileId
                && item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono profilu CSV.");
        if (!profile.IsActive || !profile.WhConfirmed)
        {
            throw new InvalidOperationException("Import wymaga aktywnego profilu z potwierdzoną jednostką Wh.");
        }

        var parsed = await parser.ParseAsync(csv, ToSnapshot(profile), ChargingImportLimits.Default, cancellationToken);
        var existing = await dbContext.ChargingImportBatches.AsNoTracking().SingleOrDefaultAsync(
            item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.FileSha256 == parsed.FileSha256,
            cancellationToken);
        if (existing is not null)
        {
            return ReplaySnapshot(existing);
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            existing = await dbContext.ChargingImportBatches.AsNoTracking().SingleOrDefaultAsync(
                item => item.CompanyId == companyId
                    && item.OwnerUserId == ownerUserId
                    && item.FileSha256 == parsed.FileSha256,
                cancellationToken);
            if (existing is not null)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return ReplaySnapshot(existing);
            }

            var uniqueRows = parsed.Rows
                .GroupBy(item => item.NormalizedRowHash, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var hashes = uniqueRows.Select(item => item.NormalizedRowHash).ToArray();
            var existingHashes = await dbContext.ChargingSessions.AsNoTracking()
                .Where(item => item.CompanyId == companyId && hashes.Contains(item.NormalizedRowHash))
                .Select(item => item.NormalizedRowHash)
                .ToListAsync(cancellationToken);
            var known = existingHashes.ToHashSet(StringComparer.Ordinal);
            var newRows = uniqueRows.Where(item => !known.Contains(item.NormalizedRowHash)).ToArray();
            var batch = ChargingImportBatch.Create(
                companyId,
                ownerUserId,
                profileId,
                parsed.FileSha256,
                parsed.Rows.Count,
                newRows.Length,
                nowUtc);
            dbContext.ChargingImportBatches.Add(batch);
            foreach (var row in newRows)
            {
                dbContext.ChargingSessions.Add(ChargingSession.Create(
                    companyId,
                    ownerUserId,
                    batch.Id,
                    row.LocalMonth,
                    row.StartedAtUtc,
                    row.OriginalTimestamp,
                    row.EnergyWh,
                    row.NormalizedRowHash,
                    row.SourceIdentity,
                    nowUtc));
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new ChargingImportSnapshot(
                    batch.Id,
                    batch.ParsedRows,
                    batch.AddedRows,
                    batch.SkippedRows,
                    IsReplay: false);
            }
            catch (DbUpdateException exception)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                dbContext.ChangeTracker.Clear();
                var replay = await dbContext.ChargingImportBatches.AsNoTracking().SingleOrDefaultAsync(
                    item => item.CompanyId == companyId
                        && item.OwnerUserId == ownerUserId
                        && item.FileSha256 == parsed.FileSha256,
                    cancellationToken);
                if (replay is not null)
                {
                    return ReplaySnapshot(replay);
                }

                if (attempt == 3)
                {
                    throw new InvalidOperationException(
                        "Nie udało się bezpiecznie scalić równoległych importów CSV. Spróbuj ponownie.",
                        exception);
                }
            }
        }

        throw new InvalidOperationException("Import CSV nie zakończył się wynikiem.");
    }

    public async Task<ChargingReportSnapshot> GenerateReportAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedMonth = new DateOnly(month.Year, month.Month, 1);
        var company = await dbContext.Companies
            .Include(item => item.EnergyRates)
            .SingleOrDefaultAsync(item => item.Id == companyId && item.OwnerUserId == ownerUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono firmy.");
        var sessions = await dbContext.ChargingSessions
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.LocalMonth == normalizedMonth)
            .OrderBy(item => item.StartedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var latest = await dbContext.ChargingReports
            .Include(item => item.Sessions)
            .Where(item => item.CompanyId == companyId
                && item.OwnerUserId == ownerUserId
                && item.Month == normalizedMonth)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var rate = company.GetEnergyRate(normalizedMonth);
        var candidate = ChargingReport.Create(
            companyId,
            ownerUserId,
            normalizedMonth,
            rate.Id,
            rate.GrossPricePerKwh,
            sessions,
            (latest?.VersionNumber ?? 0) + 1,
            latest?.Id,
            nowUtc);
        if (latest?.InputFingerprint == candidate.InputFingerprint)
        {
            return ToSnapshot(latest);
        }

        dbContext.ChargingReports.Add(candidate);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToSnapshot(candidate);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var replay = await dbContext.ChargingReports.AsNoTracking()
                .Include(item => item.Sessions)
                .SingleOrDefaultAsync(
                    item => item.CompanyId == companyId
                        && item.OwnerUserId == ownerUserId
                        && item.Month == normalizedMonth
                        && item.InputFingerprint == candidate.InputFingerprint,
                    cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return ToSnapshot(replay);
        }
    }

    public async Task<ChargingMonthSnapshot?> GetMonthAsync(
        string ownerUserId,
        Guid companyId,
        DateOnly month,
        CancellationToken cancellationToken = default)
    {
        var ownsCompany = await dbContext.Companies.AsNoTracking().AnyAsync(
            item => item.Id == companyId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        if (!ownsCompany)
        {
            return null;
        }

        var normalizedMonth = new DateOnly(month.Year, month.Month, 1);
        var profile = await dbContext.ChargingCsvProfiles.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var sessionCount = await dbContext.ChargingSessions.AsNoTracking().CountAsync(
            item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId && item.LocalMonth == normalizedMonth,
            cancellationToken);
        var batchCount = await dbContext.ChargingImportBatches.AsNoTracking().CountAsync(
            item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId,
            cancellationToken);
        var latestReport = await dbContext.ChargingReports.AsNoTracking()
            .Include(item => item.Sessions)
            .Where(item => item.CompanyId == companyId && item.OwnerUserId == ownerUserId && item.Month == normalizedMonth)
            .OrderByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return new ChargingMonthSnapshot(
            profile is null ? null : ToSnapshot(profile),
            normalizedMonth,
            sessionCount,
            batchCount,
            latestReport is null ? null : ToSnapshot(latestReport));
    }

    public async Task<ChargingReportSnapshot?> GetReportAsync(
        string ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.ChargingReports.AsNoTracking()
            .Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == reportId && item.OwnerUserId == ownerUserId, cancellationToken);
        return report is null ? null : ToSnapshot(report);
    }

    private async Task EnsureOwnedCompanyAsync(string ownerUserId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AsNoTracking().AnyAsync(
                item => item.Id == companyId && item.OwnerUserId == ownerUserId,
                cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono firmy.");
        }
    }

    private static bool SameConfiguration(ChargingCsvProfile profile, SaveChargingProfileCommand command)
        => profile.Separator == command.Separator
            && profile.TimestampColumn == command.TimestampColumn.Trim()
            && profile.EnergyWhColumn == command.EnergyWhColumn.Trim()
            && profile.DateFormat == command.DateFormat.Trim()
            && profile.TimeZoneId == command.TimeZoneId.Trim()
            && profile.IdentityColumn == (string.IsNullOrWhiteSpace(command.IdentityColumn) ? null : command.IdentityColumn.Trim());

    private static ChargingProfileSnapshot ToSnapshot(ChargingCsvProfile profile)
        => new(
            profile.Id,
            profile.CompanyId,
            profile.VersionNumber,
            profile.Separator,
            profile.TimestampColumn,
            profile.EnergyWhColumn,
            profile.DateFormat,
            profile.TimeZoneId,
            profile.IdentityColumn,
            profile.WhConfirmed,
            profile.IsActive);

    private static ChargingImportSnapshot ReplaySnapshot(ChargingImportBatch batch)
        => new(batch.Id, batch.ParsedRows, 0, batch.ParsedRows, IsReplay: true);

    private static ChargingReportSnapshot ToSnapshot(ChargingReport report)
        => new(
            report.Id,
            report.CompanyId,
            report.Month,
            report.EnergyRatePeriodId,
            report.GrossRatePerKwh,
            report.VersionNumber,
            report.PreviousReportId,
            report.TotalWh,
            report.TotalKwh,
            report.GrossCost,
            report.TaxStatus,
            report.Sessions.Count,
            report.CreatedAtUtc);
}
