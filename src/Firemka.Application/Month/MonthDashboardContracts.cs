using Firemka.Application.MonthClosing;
using Firemka.Domain.Calculations;

namespace Firemka.Application.Month;

public sealed record MonthDashboard(
    int Year,
    int Month,
    bool IsCompanyConfigured,
    Guid? CompanyId,
    string? CompanyName,
    decimal? SubscriptionNetMonthlyAmount,
    int DocumentsToReview,
    int RulesToDefine,
    int ErrorsToResolve,
    bool IsTaxEstimateAvailable,
    decimal? PitAdvanceEstimate,
    decimal? VatPaymentEstimate,
    decimal? HealthContributionEstimate,
    CalculationRuleTrust? RuleSetTrust,
    CalculationTrust? EstimateTrust,
    MonthSettlementStatus? SettlementStatus,
    MonthClosingBlocker? NextBlocker,
    IReadOnlyList<MonthDashboardPayment>? Payments = null,
    DateOnly? NextDueDate = null,
    string BackupStatus = "Kopie nie są jeszcze skonfigurowane");

public sealed record MonthDashboardPayment(
    string Label,
    decimal Amount,
    DateOnly DueOn,
    bool IsPaid);

public interface IMonthDashboardQuery
{
    Task<MonthDashboard> GetAsync(
        string ownerUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
