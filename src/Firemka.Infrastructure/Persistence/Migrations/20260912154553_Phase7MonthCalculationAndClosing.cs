using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7MonthCalculationAndClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalculationRuleSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousRuleSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PitThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitLowerRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PitHigherRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PitReducingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitPaymentOptionThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    HealthMinimumChangeMonth = table.Column<int>(type: "integer", nullable: false),
                    HealthMinimumBeforeChange = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthMinimumFromChange = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OfficialSources = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CapturedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Trust = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IndependentEvidenceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ConfirmedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RuleFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalculationRuleSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalculationRuleSets_CalculationRuleSets_PreviousRuleSetId",
                        column: x => x.PreviousRuleSetId,
                        principalTable: "CalculationRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalculationRuleSets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SocialContributionsDeductible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitBaseAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthIncomeAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningVatCarryForward = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OpeningPitAdvancesDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OpeningBalancesConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    HealthIncomeConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ValueFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthDeclarations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthDeclarations_MonthDeclarations_PreviousDeclarationId",
                        column: x => x.PreviousDeclarationId,
                        principalTable: "MonthDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthTaxAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValueFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthTaxAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthTaxAdjustments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthTaxAdjustments_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthTaxAdjustments_SourceDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "SourceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthCalculations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclarationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousMonthCalculationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevenueMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostsMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SocialContributionsMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitBaseAdjustmentMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthIncomeAdjustmentMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RevenueYtd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostsYtd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SocialContributionsYtd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdjustmentsYtd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitIncomeYtd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitTaxBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CumulativePitTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorPitAdvancesDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdvanceDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CanDeferPitPayment = table.Column<bool>(type: "boolean", nullable: false),
                    OutputVatMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InputVatMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorVatCarryForward = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPayable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatPayableRounded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatCarryForward = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentHealthIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousMonthHealthIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthMinimumBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthBasis = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthContribution = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Trust = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthCalculations_CalculationRuleSets_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "CalculationRuleSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthCalculations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthCalculations_MonthCalculations_PreviousMonthCalculatio~",
                        column: x => x.PreviousMonthCalculationId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthCalculations_MonthCalculations_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthCalculations_MonthDeclarations_DeclarationId",
                        column: x => x.DeclarationId,
                        principalTable: "MonthDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonthCalculationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthCalculationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsFormula = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthCalculationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthCalculationLines_MonthCalculations_MonthCalculationId",
                        column: x => x.MonthCalculationId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousSettlementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CalculationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthSettlements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthSettlements_MonthCalculations_CalculationId",
                        column: x => x.CalculationId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonthSettlements_MonthSettlements_PreviousSettlementId",
                        column: x => x.PreviousSettlementId,
                        principalTable: "MonthSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalculationRuleSets_CompanyId_TaxYear_VersionNumber",
                table: "CalculationRuleSets",
                columns: new[] { "CompanyId", "TaxYear", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalculationRuleSets_PreviousRuleSetId",
                table: "CalculationRuleSets",
                column: "PreviousRuleSetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculationLines_MonthCalculationId_Sequence",
                table: "MonthCalculationLines",
                columns: new[] { "MonthCalculationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_CompanyId_Month_InputFingerprint",
                table: "MonthCalculations",
                columns: new[] { "CompanyId", "Month", "InputFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_CompanyId_Month_VersionNumber",
                table: "MonthCalculations",
                columns: new[] { "CompanyId", "Month", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_DeclarationId",
                table: "MonthCalculations",
                column: "DeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_PreviousMonthCalculationId",
                table: "MonthCalculations",
                column: "PreviousMonthCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_PreviousVersionId",
                table: "MonthCalculations",
                column: "PreviousVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthCalculations_RuleSetId",
                table: "MonthCalculations",
                column: "RuleSetId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthDeclarations_CompanyId_Month_VersionNumber",
                table: "MonthDeclarations",
                columns: new[] { "CompanyId", "Month", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthDeclarations_PreviousDeclarationId",
                table: "MonthDeclarations",
                column: "PreviousDeclarationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthSettlements_CalculationId",
                table: "MonthSettlements",
                column: "CalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthSettlements_CompanyId_Month_VersionNumber",
                table: "MonthSettlements",
                columns: new[] { "CompanyId", "Month", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthSettlements_PreviousSettlementId",
                table: "MonthSettlements",
                column: "PreviousSettlementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthTaxAdjustments_CompanyId_Month_CreatedAtUtc",
                table: "MonthTaxAdjustments",
                columns: new[] { "CompanyId", "Month", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthTaxAdjustments_CompanyId_Month_ValueFingerprint",
                table: "MonthTaxAdjustments",
                columns: new[] { "CompanyId", "Month", "ValueFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthTaxAdjustments_SalesInvoiceId",
                table: "MonthTaxAdjustments",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthTaxAdjustments_SourceDocumentId",
                table: "MonthTaxAdjustments",
                column: "SourceDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthCalculationLines");

            migrationBuilder.DropTable(
                name: "MonthSettlements");

            migrationBuilder.DropTable(
                name: "MonthTaxAdjustments");

            migrationBuilder.DropTable(
                name: "MonthCalculations");

            migrationBuilder.DropTable(
                name: "CalculationRuleSets");

            migrationBuilder.DropTable(
                name: "MonthDeclarations");
        }
    }
}
