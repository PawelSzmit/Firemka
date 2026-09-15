using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations;

public partial class Phase6CostAccountingVehiclesCharging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ChargingCsvProfiles", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> versionNumber = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> previousProfileId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> separator = table.Column<char>("character(1)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 160;
            OperationBuilder<AddColumnOperation> timestampColumn = table.Column<string>("character varying(160)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 160;
            OperationBuilder<AddColumnOperation> energyWhColumn = table.Column<string>("character varying(160)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 120;
            OperationBuilder<AddColumnOperation> dateFormat = table.Column<string>("character varying(120)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 120;
            OperationBuilder<AddColumnOperation> timeZoneId = table.Column<string>("character varying(120)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 160;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                VersionNumber = versionNumber,
                PreviousProfileId = previousProfileId,
                Separator = separator,
                TimestampColumn = timestampColumn,
                EnergyWhColumn = energyWhColumn,
                DateFormat = dateFormat,
                TimeZoneId = timeZoneId,
                IdentityColumn = table.Column<string>("character varying(160)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                WhConfirmed = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                IsActive = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ActivatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                DeactivatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ConcurrencyStamp = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_ChargingCsvProfiles", x => (object)x.Id);
            table.ForeignKey("FK_ChargingCsvProfiles_ChargingCsvProfiles_PreviousProfileId", x => (object)x.PreviousProfileId, "ChargingCsvProfiles", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_ChargingCsvProfiles_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("ChargingReports", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> month = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> energyRatePeriodId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 18;
            int? num2 = 4;
            OperationBuilder<AddColumnOperation> grossRatePerKwh = table.Column<decimal>("numeric(18,4)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            OperationBuilder<AddColumnOperation> versionNumber = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> previousReportId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 18;
            num = 3;
            OperationBuilder<AddColumnOperation> totalWh = table.Column<decimal>("numeric(18,3)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 18;
            num2 = 3;
            OperationBuilder<AddColumnOperation> totalKwh = table.Column<decimal>("numeric(18,3)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 18;
            num = 2;
            OperationBuilder<AddColumnOperation> grossCost = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 160;
            OperationBuilder<AddColumnOperation> taxStatus = table.Column<string>("character varying(160)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 64;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                Month = month,
                EnergyRatePeriodId = energyRatePeriodId,
                GrossRatePerKwh = grossRatePerKwh,
                VersionNumber = versionNumber,
                PreviousReportId = previousReportId,
                TotalWh = totalWh,
                TotalKwh = totalKwh,
                GrossCost = grossCost,
                TaxStatus = taxStatus,
                InputFingerprint = table.Column<string>("character varying(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_ChargingReports", x => (object)x.Id);
            table.ForeignKey("FK_ChargingReports_ChargingReports_PreviousReportId", x => (object)x.PreviousReportId, "ChargingReports", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_ChargingReports_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_ChargingReports_EnergyRatePeriod_EnergyRatePeriodId", x => (object)x.EnergyRatePeriodId, "EnergyRatePeriod", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("CostRules", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 64;
            OperationBuilder<AddColumnOperation> fingerprintKey = table.Column<string>("character varying(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> versionNumber = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> previousRuleId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 320;
            OperationBuilder<AddColumnOperation> sellerKey = table.Column<string>("character varying(320)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 2;
            OperationBuilder<AddColumnOperation> sellerCountryCode = table.Column<string>("character varying(2)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 3;
            OperationBuilder<AddColumnOperation> currency = table.Column<string>("character varying(3)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 32;
            OperationBuilder<AddColumnOperation> vatTreatment = table.Column<string>("character varying(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 5;
            int? num2 = 2;
            OperationBuilder<AddColumnOperation> vatRate = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 40;
            OperationBuilder<AddColumnOperation> serviceKind = table.Column<string>("character varying(40)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 120;
            OperationBuilder<AddColumnOperation> kpirCategory = table.Column<string>("character varying(120)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 5;
            num = 2;
            OperationBuilder<AddColumnOperation> vatDeductionPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 5;
            num2 = 2;
            OperationBuilder<AddColumnOperation> kpirCostPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 24;
            OperationBuilder<AddColumnOperation> kpirPeriodPolicy = table.Column<string>("character varying(24)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 24;
            OperationBuilder<AddColumnOperation> vatPeriodPolicy = table.Column<string>("character varying(24)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 1000;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                FingerprintKey = fingerprintKey,
                VersionNumber = versionNumber,
                PreviousRuleId = previousRuleId,
                SellerKey = sellerKey,
                SellerCountryCode = sellerCountryCode,
                Currency = currency,
                VatTreatment = vatTreatment,
                VatRate = vatRate,
                ServiceKind = serviceKind,
                KpirCategory = kpirCategory,
                VatDeductionPercent = vatDeductionPercent,
                KpirCostPercent = kpirCostPercent,
                KpirPeriodPolicy = kpirPeriodPolicy,
                VatPeriodPolicy = vatPeriodPolicy,
                DecisionSource = table.Column<string>("character varying(1000)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                IsActive = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                DeactivatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_CostRules", x => (object)x.Id);
            table.ForeignKey("FK_CostRules_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_CostRules_CostRules_PreviousRuleId", x => (object)x.PreviousRuleId, "CostRules", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("VehicleCostPolicies", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 32;
            OperationBuilder<AddColumnOperation> kind = table.Column<string>("character varying(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> effectiveFromMonth = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> versionNumber = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> previousPolicyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 5;
            int? num2 = 2;
            OperationBuilder<AddColumnOperation> vatDeductionPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 5;
            num = 2;
            OperationBuilder<AddColumnOperation> kpirCostPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 1000;
            OperationBuilder<AddColumnOperation> evidenceReference = table.Column<string>("character varying(1000)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 24;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                Kind = kind,
                EffectiveFromMonth = effectiveFromMonth,
                VersionNumber = versionNumber,
                PreviousPolicyId = previousPolicyId,
                VatDeductionPercent = vatDeductionPercent,
                KpirCostPercent = kpirCostPercent,
                EvidenceReference = evidenceReference,
                Status = table.Column<string>("character varying(24)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ActivatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                DeactivatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ConcurrencyStamp = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_VehicleCostPolicies", x => (object)x.Id);
            table.ForeignKey("FK_VehicleCostPolicies_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_VehicleCostPolicies_VehicleCostPolicies_PreviousPolicyId", x => (object)x.PreviousPolicyId, "VehicleCostPolicies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("ChargingImportBatches", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> profileId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 64;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                ProfileId = profileId,
                FileSha256 = table.Column<string>("character varying(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ParsedRows = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                AddedRows = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                SkippedRows = table.Column<int>("integer", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CompletedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_ChargingImportBatches", x => (object)x.Id);
            table.ForeignKey("FK_ChargingImportBatches_ChargingCsvProfiles_ProfileId", x => (object)x.ProfileId, "ChargingCsvProfiles", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_ChargingImportBatches_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("CostBookings", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> sourceDocumentId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 32;
            OperationBuilder<AddColumnOperation> status = table.Column<string>("character varying(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 64;
            OperationBuilder<AddColumnOperation> fingerprintKey = table.Column<string>("character varying(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 320;
            OperationBuilder<AddColumnOperation> sellerKey = table.Column<string>("character varying(320)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 2;
            OperationBuilder<AddColumnOperation> sellerCountryCode = table.Column<string>("character varying(2)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 3;
            OperationBuilder<AddColumnOperation> currency = table.Column<string>("character varying(3)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 32;
            OperationBuilder<AddColumnOperation> vatTreatment = table.Column<string>("character varying(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 5;
            int? num2 = 2;
            OperationBuilder<AddColumnOperation> vatRate = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 40;
            OperationBuilder<AddColumnOperation> serviceKind = table.Column<string>("character varying(40)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 18;
            num = 2;
            OperationBuilder<AddColumnOperation> grossAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 18;
            num2 = 2;
            OperationBuilder<AddColumnOperation> inputVatAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            OperationBuilder<AddColumnOperation> issueDate = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> ruleId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 120;
            OperationBuilder<AddColumnOperation> kpirCategory = table.Column<string>("character varying(120)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 5;
            num = 2;
            OperationBuilder<AddColumnOperation> vatDeductionPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 5;
            num2 = 2;
            OperationBuilder<AddColumnOperation> kpirCostPercent = table.Column<decimal>("numeric(5,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            OperationBuilder<AddColumnOperation> kpirPeriod = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> vatPeriod = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 18;
            num = 2;
            OperationBuilder<AddColumnOperation> deductibleVatAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null);
            num = 18;
            num2 = 2;
            OperationBuilder<AddColumnOperation> kpirAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 1000;
            return new
            {
                Id = id,
                SourceDocumentId = sourceDocumentId,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                Status = status,
                FingerprintKey = fingerprintKey,
                SellerKey = sellerKey,
                SellerCountryCode = sellerCountryCode,
                Currency = currency,
                VatTreatment = vatTreatment,
                VatRate = vatRate,
                ServiceKind = serviceKind,
                GrossAmount = grossAmount,
                InputVatAmount = inputVatAmount,
                IssueDate = issueDate,
                RuleId = ruleId,
                KpirCategory = kpirCategory,
                VatDeductionPercent = vatDeductionPercent,
                KpirCostPercent = kpirCostPercent,
                KpirPeriod = kpirPeriod,
                VatPeriod = vatPeriod,
                DeductibleVatAmount = deductibleVatAmount,
                KpirAmount = kpirAmount,
                Explanation = table.Column<string>("character varying(1000)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                Automatic = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                BookedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                ConcurrencyStamp = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_CostBookings", x => (object)x.Id);
            table.ForeignKey("FK_CostBookings_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_CostBookings_CostRules_RuleId", x => (object)x.RuleId, "CostRules", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_CostBookings_SourceDocuments_SourceDocumentId", x => (object)x.SourceDocumentId, "SourceDocuments", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("ChargingSessions", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> companyId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> firstImportBatchId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> localMonth = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> startedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 160;
            OperationBuilder<AddColumnOperation> originalTimestamp = table.Column<string>("character varying(160)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 18;
            int? num2 = 3;
            OperationBuilder<AddColumnOperation> energyWh = table.Column<decimal>("numeric(18,3)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 64;
            OperationBuilder<AddColumnOperation> normalizedRowHash = table.Column<string>("character varying(64)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num2 = 500;
            return new
            {
                Id = id,
                CompanyId = companyId,
                OwnerUserId = ownerUserId,
                FirstImportBatchId = firstImportBatchId,
                LocalMonth = localMonth,
                StartedAtUtc = startedAtUtc,
                OriginalTimestamp = originalTimestamp,
                EnergyWh = energyWh,
                NormalizedRowHash = normalizedRowHash,
                SourceIdentity = table.Column<string>("character varying(500)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_ChargingSessions", x => (object)x.Id);
            table.ForeignKey("FK_ChargingSessions_ChargingImportBatches_FirstImportBatchId", x => (object)x.FirstImportBatchId, "ChargingImportBatches", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_ChargingSessions_Companies_CompanyId", x => (object)x.CompanyId, "Companies", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("KpirEntries", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> bookingId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> sourceDocumentId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> period = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 120;
            OperationBuilder<AddColumnOperation> category = table.Column<string>("character varying(120)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 18;
            int? num2 = 2;
            return new
            {
                Id = id,
                BookingId = bookingId,
                SourceDocumentId = sourceDocumentId,
                OwnerUserId = ownerUserId,
                Period = period,
                Category = category,
                Amount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null),
                Included = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                RuleId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_KpirEntries", x => (object)x.Id);
            table.ForeignKey("FK_KpirEntries_CostBookings_BookingId", x => (object)x.BookingId, "CostBookings", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_KpirEntries_CostRules_RuleId", x => (object)x.RuleId, "CostRules", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_KpirEntries_SourceDocuments_SourceDocumentId", x => (object)x.SourceDocumentId, "SourceDocuments", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("VatPurchaseEntries", delegate (ColumnsBuilder table)
        {
            OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> bookingId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> sourceDocumentId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            int? num = 450;
            OperationBuilder<AddColumnOperation> ownerUserId = table.Column<string>("character varying(450)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            OperationBuilder<AddColumnOperation> period = table.Column<DateOnly>("date", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
            num = 18;
            int? num2 = 2;
            OperationBuilder<AddColumnOperation> inputVatAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num, num2, (bool?)null);
            num2 = 18;
            num = 2;
            return new
            {
                Id = id,
                BookingId = bookingId,
                SourceDocumentId = sourceDocumentId,
                OwnerUserId = ownerUserId,
                Period = period,
                InputVatAmount = inputVatAmount,
                DeductibleVatAmount = table.Column<decimal>("numeric(18,2)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, num2, num, (bool?)null),
                Included = table.Column<bool>("boolean", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                RuleId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
                CreatedAtUtc = table.Column<DateTimeOffset>("timestamp with time zone", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
            };
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_VatPurchaseEntries", x => (object)x.Id);
            table.ForeignKey("FK_VatPurchaseEntries_CostBookings_BookingId", x => (object)x.BookingId, "CostBookings", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_VatPurchaseEntries_CostRules_RuleId", x => (object)x.RuleId, "CostRules", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
            table.ForeignKey("FK_VatPurchaseEntries_SourceDocuments_SourceDocumentId", x => (object)x.SourceDocumentId, "SourceDocuments", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateTable("ChargingReportSessions", (ColumnsBuilder table) => new
        {
            ChargingReportId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
            ChargingSessionId = table.Column<Guid>("uuid", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
        }, (string)null, table =>
        {
            table.PrimaryKey("PK_ChargingReportSessions", x => (object)new { x.ChargingReportId, x.ChargingSessionId });
            table.ForeignKey("FK_ChargingReportSessions_ChargingReports_ChargingReportId", x => (object)x.ChargingReportId, "ChargingReports", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)2);
            table.ForeignKey("FK_ChargingReportSessions_ChargingSessions_ChargingSessionId", x => (object)x.ChargingSessionId, "ChargingSessions", "Id", (string)null, (ReferentialAction)0, (ReferentialAction)1);
        }, (string)null);
        migrationBuilder.CreateIndex("IX_ChargingCsvProfiles_CompanyId", "ChargingCsvProfiles", "CompanyId", (string)null, true, "\"IsActive\" = TRUE", (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingCsvProfiles_CompanyId_VersionNumber", "ChargingCsvProfiles", new string[2] { "CompanyId", "VersionNumber" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingCsvProfiles_PreviousProfileId", "ChargingCsvProfiles", "PreviousProfileId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingImportBatches_CompanyId_FileSha256", "ChargingImportBatches", new string[2] { "CompanyId", "FileSha256" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingImportBatches_OwnerUserId_CompletedAtUtc", "ChargingImportBatches", new string[2] { "OwnerUserId", "CompletedAtUtc" }, (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingImportBatches_ProfileId", "ChargingImportBatches", "ProfileId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingReports_CompanyId_Month_InputFingerprint", "ChargingReports", new string[3] { "CompanyId", "Month", "InputFingerprint" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingReports_CompanyId_Month_VersionNumber", "ChargingReports", new string[3] { "CompanyId", "Month", "VersionNumber" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingReports_EnergyRatePeriodId", "ChargingReports", "EnergyRatePeriodId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingReports_PreviousReportId", "ChargingReports", "PreviousReportId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingReportSessions_ChargingSessionId", "ChargingReportSessions", "ChargingSessionId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingSessions_CompanyId_NormalizedRowHash", "ChargingSessions", new string[2] { "CompanyId", "NormalizedRowHash" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingSessions_FirstImportBatchId", "ChargingSessions", "FirstImportBatchId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_ChargingSessions_OwnerUserId_LocalMonth_StartedAtUtc", "ChargingSessions", new string[3] { "OwnerUserId", "LocalMonth", "StartedAtUtc" }, (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostBookings_CompanyId", "CostBookings", "CompanyId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostBookings_OwnerUserId_Status", "CostBookings", new string[2] { "OwnerUserId", "Status" }, (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostBookings_RuleId", "CostBookings", "RuleId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostBookings_SourceDocumentId", "CostBookings", "SourceDocumentId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostRules_CompanyId_FingerprintKey", "CostRules", new string[2] { "CompanyId", "FingerprintKey" }, (string)null, true, "\"IsActive\" = TRUE", (bool[])null);
        migrationBuilder.CreateIndex("IX_CostRules_CompanyId_FingerprintKey_VersionNumber", "CostRules", new string[3] { "CompanyId", "FingerprintKey", "VersionNumber" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_CostRules_PreviousRuleId", "CostRules", "PreviousRuleId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_KpirEntries_BookingId", "KpirEntries", "BookingId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_KpirEntries_OwnerUserId_Period", "KpirEntries", new string[2] { "OwnerUserId", "Period" }, (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_KpirEntries_RuleId", "KpirEntries", "RuleId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_KpirEntries_SourceDocumentId", "KpirEntries", "SourceDocumentId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VatPurchaseEntries_BookingId", "VatPurchaseEntries", "BookingId", (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VatPurchaseEntries_OwnerUserId_Period", "VatPurchaseEntries", new string[2] { "OwnerUserId", "Period" }, (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VatPurchaseEntries_RuleId", "VatPurchaseEntries", "RuleId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VatPurchaseEntries_SourceDocumentId", "VatPurchaseEntries", "SourceDocumentId", (string)null, false, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VehicleCostPolicies_CompanyId_Kind_EffectiveFromMonth", "VehicleCostPolicies", new string[3] { "CompanyId", "Kind", "EffectiveFromMonth" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VehicleCostPolicies_CompanyId_Kind_VersionNumber", "VehicleCostPolicies", new string[3] { "CompanyId", "Kind", "VersionNumber" }, (string)null, true, (string)null, (bool[])null);
        migrationBuilder.CreateIndex("IX_VehicleCostPolicies_PreviousPolicyId", "VehicleCostPolicies", "PreviousPolicyId", (string)null, true, (string)null, (bool[])null);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ChargingReportSessions", (string)null);
        migrationBuilder.DropTable("KpirEntries", (string)null);
        migrationBuilder.DropTable("VatPurchaseEntries", (string)null);
        migrationBuilder.DropTable("VehicleCostPolicies", (string)null);
        migrationBuilder.DropTable("ChargingReports", (string)null);
        migrationBuilder.DropTable("ChargingSessions", (string)null);
        migrationBuilder.DropTable("CostBookings", (string)null);
        migrationBuilder.DropTable("ChargingImportBatches", (string)null);
        migrationBuilder.DropTable("CostRules", (string)null);
        migrationBuilder.DropTable("ChargingCsvProfiles", (string)null);
    }
}
