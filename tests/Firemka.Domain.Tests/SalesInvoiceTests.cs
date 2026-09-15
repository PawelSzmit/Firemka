using Firemka.Domain.Sales;

namespace Firemka.Domain.Tests;

public sealed class SalesInvoiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    [Fact]
    public void Draft_always_covers_the_full_service_month_without_proration()
    {
        var invoice = CreateDraft(new DateOnly(2026, 10, 15));

        Assert.Equal(new DateOnly(2026, 10, 1), invoice.ServicePeriodFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), invoice.ServicePeriodTo);
        Assert.Equal(1_500m, invoice.NetAmount);
        Assert.Equal(345m, invoice.VatAmount);
        Assert.Equal(1_845m, invoice.GrossAmount);
    }

    [Fact]
    public void Approval_in_the_same_month_uses_the_actual_issue_date_and_seven_day_due_date()
    {
        var invoice = CreateDraft(new DateOnly(2026, 10, 1));

        var warning = invoice.PrepareForIssue(
            new DateOnly(2026, 10, 6),
            "FV/2026/10/001",
            automatic: false,
            "<Faktura />",
            CreatedAtUtc.AddDays(16));

        Assert.False(warning.IsLateApproval);
        Assert.Equal(new DateOnly(2026, 10, 6), invoice.IssueDate);
        Assert.Equal(new DateOnly(2026, 10, 13), invoice.PaymentDueDate);
        Assert.Equal(new DateOnly(2026, 10, 1), invoice.ServicePeriodFrom);
        Assert.Equal(new DateOnly(2026, 10, 31), invoice.ServicePeriodTo);
    }

    [Fact]
    public void Approval_in_a_later_month_keeps_the_original_period_and_returns_a_warning()
    {
        var invoice = CreateDraft(new DateOnly(2026, 9, 1));

        var warning = invoice.PrepareForIssue(
            new DateOnly(2026, 10, 2),
            "FV/2026/10/001",
            automatic: false,
            "<Faktura />",
            CreatedAtUtc.AddDays(12));

        Assert.True(warning.IsLateApproval);
        Assert.Equal(new DateOnly(2026, 10, 2), invoice.IssueDate);
        Assert.Equal(new DateOnly(2026, 10, 9), invoice.PaymentDueDate);
        Assert.Equal(new DateOnly(2026, 9, 1), invoice.ServicePeriodFrom);
        Assert.Equal(new DateOnly(2026, 9, 30), invoice.ServicePeriodTo);
    }

    [Fact]
    public void Editing_one_draft_does_not_change_its_subscription_rate_reference()
    {
        var rateId = Guid.NewGuid();
        var invoice = CreateDraft(new DateOnly(2026, 10, 1), rateId);

        invoice.EditDraft(1_650m, 23m, CreatedAtUtc.AddDays(1));

        Assert.True(invoice.HasManualAmountOverride);
        Assert.Equal(1_650m, invoice.NetAmount);
        Assert.Equal(rateId, invoice.SubscriptionRatePeriodId);
    }

    [Fact]
    public void Future_rate_does_not_silently_replace_a_manually_edited_draft()
    {
        var invoice = CreateDraft(new DateOnly(2026, 10, 1));
        invoice.EditDraft(1_650m, 23m, CreatedAtUtc.AddDays(1));
        var futureRateId = Guid.NewGuid();

        var result = invoice.ApplySubscriptionRate(
            futureRateId,
            1_800m,
            23m,
            replaceManualOverride: false,
            CreatedAtUtc.AddDays(2));

        Assert.Equal(SubscriptionRateApplication.RequiresConfirmation, result);
        Assert.Equal(1_650m, invoice.NetAmount);
        Assert.Equal(1_800m, invoice.PendingSubscriptionNetAmount);
        Assert.Equal(futureRateId, invoice.PendingSubscriptionRatePeriodId);
    }

    [Fact]
    public void Confirmed_future_rate_replaces_a_manual_override_but_never_an_issued_invoice()
    {
        var invoice = CreateDraft(new DateOnly(2026, 10, 1));
        invoice.EditDraft(1_650m, 23m, CreatedAtUtc.AddDays(1));
        var futureRateId = Guid.NewGuid();

        var result = invoice.ApplySubscriptionRate(
            futureRateId,
            1_800m,
            23m,
            replaceManualOverride: true,
            CreatedAtUtc.AddDays(2));

        Assert.Equal(SubscriptionRateApplication.Applied, result);
        Assert.False(invoice.HasManualAmountOverride);
        Assert.Equal(1_800m, invoice.NetAmount);

        invoice.PrepareForIssue(
            new DateOnly(2026, 10, 2),
            "FV/2026/10/001",
            automatic: false,
            "<Faktura />",
            CreatedAtUtc.AddDays(3));
        invoice.MarkIssued(
            "20261002-TEST-KSEF-01",
            "<UPO />",
            "<Faktura />",
            CreatedAtUtc.AddDays(3));

        Assert.Throws<InvalidOperationException>(() => invoice.ApplySubscriptionRate(
            Guid.NewGuid(),
            2_000m,
            23m,
            replaceManualOverride: true,
            CreatedAtUtc.AddDays(4)));
    }

    private static SalesInvoice CreateDraft(DateOnly serviceMonth, Guid? rateId = null)
        => SalesInvoice.CreateDraft(
            Guid.NewGuid(),
            "owner-1",
            serviceMonth,
            rateId ?? Guid.NewGuid(),
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            CreatedAtUtc);
}
