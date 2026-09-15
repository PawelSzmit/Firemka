using Firemka.Domain.Companies;
using Firemka.Domain.TaxYears;

namespace Firemka.Domain.Tests;

public sealed class CompanyProfileTests
{
    [Fact]
    public void Registration_keeps_the_company_address_and_subscription_service_description()
    {
        var company = Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowy adres firmy 2, 00-002 Warszawa",
            new DateOnly(2026, 10, 15),
            "Testowy Klient",
            "1234563218",
            "Testowy adres klienta 1, 00-001 Warszawa",
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            DateTimeOffset.Parse("2026-09-10T10:00:00Z"));

        Assert.Equal("Testowy adres firmy 2, 00-002 Warszawa", company.Address);
        Assert.Equal("Miesięczny dostęp do aplikacji Test SaaS", company.ServiceDescription);
    }

    [Fact]
    public void Registration_in_the_middle_of_a_month_keeps_the_full_monthly_subscription()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));

        var rate = company.GetSubscriptionRate(new DateOnly(2026, 10, 1));

        Assert.Equal(1_500m, rate.NetMonthlyAmount);
        Assert.Equal(new DateOnly(2026, 10, 1), rate.ValidFromMonth);
    }

    [Fact]
    public void A_new_setting_period_does_not_change_the_value_for_an_earlier_month()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));
        company.ChangeSubscriptionRate(
            new DateOnly(2027, 1, 1),
            1_800m,
            23m,
            DateTimeOffset.Parse("2026-12-20T10:00:00Z"));
        company.ChangeEnergyRate(
            new DateOnly(2027, 1, 1),
            1.05m,
            DateTimeOffset.Parse("2026-12-20T10:00:00Z"));

        Assert.Equal(1_500m, company.GetSubscriptionRate(new DateOnly(2026, 12, 1)).NetMonthlyAmount);
        Assert.Equal(1_800m, company.GetSubscriptionRate(new DateOnly(2027, 1, 1)).NetMonthlyAmount);
        Assert.Equal(0.91m, company.GetEnergyRate(new DateOnly(2026, 12, 1)).GrossPricePerKwh);
        Assert.Equal(1.05m, company.GetEnergyRate(new DateOnly(2027, 1, 1)).GrossPricePerKwh);
    }

    [Fact]
    public void Opening_a_new_year_keeps_the_previous_year_unchanged()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));
        var previousYear = Assert.Single(company.TaxYears);

        company.OpenTaxYear(2027, DateTimeOffset.Parse("2026-12-20T10:00:00Z"));

        Assert.Equal(2026, previousYear.Year);
        Assert.Equal(TaxationForm.TaxScale, previousYear.TaxationForm);
        Assert.Collection(
            company.TaxYears.OrderBy(item => item.Year),
            item => Assert.Equal(2026, item.Year),
            item => Assert.Equal(2027, item.Year));
        Assert.All(company.TaxYears, item => Assert.Equal(TaxationForm.TaxScale, item.TaxationForm));
    }

    [Fact]
    public void Unsupported_taxation_form_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxYear.Open(
            Guid.NewGuid(),
            2026,
            (TaxationForm)999,
            DateTimeOffset.Parse("2026-09-10T10:00:00Z")));
    }

    [Fact]
    public void Setting_period_must_start_on_the_first_day_of_a_month()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));

        Assert.Throws<ArgumentException>(() => company.ChangeSubscriptionRate(
            new DateOnly(2027, 1, 15),
            1_800m,
            23m,
            DateTimeOffset.Parse("2026-12-20T10:00:00Z")));
    }

    [Fact]
    public void A_new_setting_period_must_be_later_than_the_latest_period()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));
        company.ChangeEnergyRate(
            new DateOnly(2027, 1, 1),
            1.05m,
            DateTimeOffset.Parse("2026-12-20T10:00:00Z"));

        var exception = Assert.Throws<InvalidOperationException>(() => company.ChangeEnergyRate(
            new DateOnly(2026, 12, 1),
            1.01m,
            DateTimeOffset.Parse("2026-12-21T10:00:00Z")));

        Assert.Contains("późniejszy", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vat_zus_and_vehicle_changes_use_their_own_effective_periods()
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));
        var changeDate = DateTimeOffset.Parse("2026-12-20T10:00:00Z");

        company.ChangeVatProfile(new DateOnly(2027, 1, 1), VatProfile.VatExempt, changeDate);
        company.ChangeZusProfile(new DateOnly(2027, 1, 1), ZusProfile.SocialAndHealthContributions, changeDate);
        company.ChangeVehicleProfile(new DateOnly(2027, 1, 1), VehicleArrangement.LongTermRental, changeDate);

        Assert.Equal(VatProfile.ActiveMonthly, company.GetVatProfile(new DateOnly(2026, 12, 1)).Profile);
        Assert.Equal(VatProfile.VatExempt, company.GetVatProfile(new DateOnly(2027, 1, 1)).Profile);
        Assert.Equal(
            ZusProfile.HealthOnlyDueToEmployment,
            company.GetZusProfile(new DateOnly(2026, 12, 1)).Profile);
        Assert.Equal(
            ZusProfile.SocialAndHealthContributions,
            company.GetZusProfile(new DateOnly(2027, 1, 1)).Profile);
        Assert.Equal(VehicleArrangement.None, company.GetVehicleProfile(new DateOnly(2026, 12, 1)).Arrangement);
        Assert.Equal(
            VehicleArrangement.LongTermRental,
            company.GetVehicleProfile(new DateOnly(2027, 1, 1)).Arrangement);
    }

    [Theory]
    [InlineData(2025)]
    [InlineData(2028)]
    public void Tax_years_must_be_opened_in_order(int invalidYear)
    {
        var company = CreateCompany(new DateOnly(2026, 10, 15));

        var exception = Assert.Throws<InvalidOperationException>(() => company.OpenTaxYear(
            invalidYear,
            DateTimeOffset.Parse("2026-12-20T10:00:00Z")));

        Assert.Contains("2027", exception.Message);
        Assert.Single(company.TaxYears);
    }

    private static Company CreateCompany(DateOnly businessStartDate)
    {
        return Company.Register(
            "owner-1",
            "Testowa Firma",
            "1234563218",
            "Testowy adres firmy 2, 00-002 Warszawa",
            businessStartDate,
            "Testowy Klient",
            "1234563218",
            "Testowy adres 1, 00-001 Warszawa",
            "Miesięczny dostęp do aplikacji Test SaaS",
            1_500m,
            23m,
            0.91m,
            VehicleArrangement.None,
            DateTimeOffset.Parse("2026-09-10T10:00:00Z"));
    }
}
