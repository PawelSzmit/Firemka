using Firemka.Application.Month;
using Firemka.Application.MonthClosing;
using Firemka.Application.Payments;
using Firemka.Application.Time;
using Firemka.Domain.Documents;
using Firemka.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Firemka.Infrastructure.Companies;

public sealed class MonthDashboardQuery(
    AppDbContext dbContext,
    IMonthClosingService monthClosingService,
    IPaymentService paymentService,
    TimeProvider timeProvider) : IMonthDashboardQuery
{
    public async Task<MonthDashboard> GetAsync(
        string ownerUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        var normalizedOwner = ownerUserId.Trim();
        var company = await dbContext.Companies
            .Where(item => item.OwnerUserId == normalizedOwner)
            .Include(item => item.SubscriptionRates)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return new MonthDashboard(
                year,
                month,
                false,
                null,
                null,
                null,
                0,
                0,
                0,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        var requestedMonth = new DateOnly(year, month, 1);
        var subscription = company.SubscriptionRates
            .Where(item => item.ValidFromMonth <= requestedMonth)
            .OrderByDescending(item => item.ValidFromMonth)
            .FirstOrDefault();

        var documentCounts = await dbContext.SourceDocuments
            .Where(item => item.OwnerUserId == normalizedOwner
                && item.CreatedAtUtc.Year == year
                && item.CreatedAtUtc.Month == month)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        var nowUtc = timeProvider.GetUtcNow();
        var closing = await monthClosingService.GetAsync(
            normalizedOwner,
            company.Id,
            requestedMonth,
            nowUtc,
            cancellationToken);
        var calculation = closing?.Calculation;
        var payments = await paymentService.GetAsync(
            normalizedOwner, company.Id, requestedMonth, cancellationToken);
        var paymentItems = payments.Obligations.Select(item => new MonthDashboardPayment(
            item.Label, item.AmountDue, item.DueOn, item.IsPaid)).ToArray();
        var nextDueDate = payments.Obligations.Where(item => !item.IsPaid)
            .OrderBy(item => item.DueOn).Select(item => (DateOnly?)item.DueOn).FirstOrDefault();
        var nextBlocker = closing?.Blockers.FirstOrDefault();
        if (nextBlocker is null)
        {
            var due = payments.Obligations.Where(item => !item.IsPaid)
                .OrderBy(item => item.DueOn).FirstOrDefault();
            if (due is not null)
                nextBlocker = new MonthClosingBlocker(
                    "PAYMENT_DUE",
                    $"Sprawdź płatność: {due.Label}",
                    $"/Payments?year={year}&month={month}");
        }
        var hasBackupToken = await dbContext.BackupAccessTokens.AsNoTracking().AnyAsync(
            item => item.OwnerUserId == normalizedOwner && item.RevokedAtUtc == null,
            cancellationToken);
        var backupState = await dbContext.BackupStates.AsNoTracking().SingleOrDefaultAsync(
            item => item.OwnerUserId == normalizedOwner, cancellationToken);
        var latestBackupFailed = backupState?.LastFailureCode is not null
            && (backupState.LastSuccessfulAtUtc is null
                || backupState.LastAttemptAtUtc > backupState.LastSuccessfulAtUtc);
        var backupStatus = !hasBackupToken
            ? "Kopie nie są jeszcze skonfigurowane"
            : latestBackupFailed
                ? "Ostatnia próba kopii nie powiodła się"
                : backupState?.IsOverdue(nowUtc, TimeSpan.FromHours(36), true) == true
                    ? "Kopia jest zaległa"
                    : backupState?.LastSuccessfulAtUtc is { } lastSuccess
                        ? $"Ostatnia poprawna kopia: {PolishBusinessTime.ToWarsawTime(lastSuccess):dd.MM.yyyy HH:mm}"
                        : "Oczekiwanie na pierwszą sprawdzoną kopię";

        return new MonthDashboard(
            year,
            month,
            true,
            company.Id,
            company.Name,
            subscription?.NetMonthlyAmount,
            GetCount(SourceDocumentStatus.DataToReview),
            GetCount(SourceDocumentStatus.RuleToDefine),
            GetCount(SourceDocumentStatus.ErrorToResolve),
            calculation is not null,
            calculation?.PitAdvanceDue,
            calculation?.VatPayableRounded,
            calculation?.HealthContribution,
            closing?.RuleSet?.Trust,
            calculation?.Trust,
            closing?.LatestSettlement?.Status,
            nextBlocker,
            paymentItems,
            nextDueDate,
            backupStatus);

        int GetCount(SourceDocumentStatus status)
            => documentCounts.GetValueOrDefault(status);
    }
}
