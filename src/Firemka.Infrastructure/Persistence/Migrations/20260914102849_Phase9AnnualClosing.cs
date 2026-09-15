using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AnnualClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualDeclarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousDeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpeningInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdvancesPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthContributionsPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IndependentVerificationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ConfirmedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualDeclarations_AnnualDeclarations_PreviousDeclarationId",
                        column: x => x.PreviousDeclarationId,
                        principalTable: "AnnualDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualDeclarations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnualClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousClosingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DeclarationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostsBeforeInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostsAfterInventory = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SocialContributions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdjustments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdvancesDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdvancesPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualHealthMinimumBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualHealthBasis = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualHealthContributionDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthContributionsDueMonthly = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthContributionsPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthSettlementDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthPaymentDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    JpkStoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    JpkSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JpkGeneratorVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JpkStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JpkApprovalEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JpkApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    JpkManualSubmissionReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JpkSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    JpkReceiptStoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    JpkOutcomeReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JpkOutcomeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PdfStoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PdfSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PdfGeneratorVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalResultConfirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_AnnualClosings_PreviousClosingId",
                        column: x => x.PreviousClosingId,
                        principalTable: "AnnualClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_AnnualDeclarations_DeclarationId",
                        column: x => x.DeclarationId,
                        principalTable: "AnnualDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_StoredFiles_JpkReceiptStoredFileId",
                        column: x => x.JpkReceiptStoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_StoredFiles_JpkStoredFileId",
                        column: x => x.JpkStoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosings_StoredFiles_PdfStoredFileId",
                        column: x => x.PdfStoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnualArchiveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TaxYear = table.Column<int>(type: "integer", nullable: false),
                    AnnualClosingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualArchiveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualArchiveRequests_AnnualClosings_AnnualClosingId",
                        column: x => x.AnnualClosingId,
                        principalTable: "AnnualClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnualClosingMonths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnualClosingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthCalculationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Costs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PitAdvanceDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthContributionDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualClosingMonths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualClosingMonths_AnnualClosings_AnnualClosingId",
                        column: x => x.AnnualClosingId,
                        principalTable: "AnnualClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnualClosingMonths_MonthCalculations_MonthCalculationId",
                        column: x => x.MonthCalculationId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnualClosingMonths_MonthSettlements_MonthSettlementId",
                        column: x => x.MonthSettlementId,
                        principalTable: "MonthSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualArchiveRequests_AnnualClosingId",
                table: "AnnualArchiveRequests",
                column: "AnnualClosingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosingMonths_AnnualClosingId_Month",
                table: "AnnualClosingMonths",
                columns: new[] { "AnnualClosingId", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosingMonths_MonthCalculationId",
                table: "AnnualClosingMonths",
                column: "MonthCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosingMonths_MonthSettlementId",
                table: "AnnualClosingMonths",
                column: "MonthSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_CompanyId_TaxYear_VersionNumber",
                table: "AnnualClosings",
                columns: new[] { "CompanyId", "TaxYear", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_DeclarationId",
                table: "AnnualClosings",
                column: "DeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_JpkReceiptStoredFileId",
                table: "AnnualClosings",
                column: "JpkReceiptStoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_JpkStoredFileId",
                table: "AnnualClosings",
                column: "JpkStoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_PdfStoredFileId",
                table: "AnnualClosings",
                column: "PdfStoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualClosings_PreviousClosingId",
                table: "AnnualClosings",
                column: "PreviousClosingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualDeclarations_CompanyId_TaxYear_VersionNumber",
                table: "AnnualDeclarations",
                columns: new[] { "CompanyId", "TaxYear", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualDeclarations_PreviousDeclarationId",
                table: "AnnualDeclarations",
                column: "PreviousDeclarationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualArchiveRequests");

            migrationBuilder.DropTable(
                name: "AnnualClosingMonths");

            migrationBuilder.DropTable(
                name: "AnnualClosings");

            migrationBuilder.DropTable(
                name: "AnnualDeclarations");
        }
    }
}
