using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8ManualFilings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerAddress",
                table: "SourceDocuments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FilingProfileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Pesel = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    TaxOfficeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ZusInsuranceTitleCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ConfirmationEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ConfirmedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilingProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilingProfileVersions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingProfileVersions_FilingProfileVersions_PreviousVersion~",
                        column: x => x.PreviousVersionId,
                        principalTable: "FilingProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FilingArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MonthSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalculationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilingProfileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GeneratorVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApprovalEvidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ManualSubmissionReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceiptFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutcomeReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutcomeAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilingArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_FilingArtifacts_PreviousArtifactId",
                        column: x => x.PreviousArtifactId,
                        principalTable: "FilingArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_FilingProfileVersions_FilingProfileVersionId",
                        column: x => x.FilingProfileVersionId,
                        principalTable: "FilingProfileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_MonthCalculations_CalculationId",
                        column: x => x.CalculationId,
                        principalTable: "MonthCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_MonthSettlements_MonthSettlementId",
                        column: x => x.MonthSettlementId,
                        principalTable: "MonthSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_StoredFiles_ReceiptFileId",
                        column: x => x.ReceiptFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FilingArtifacts_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_CalculationId",
                table: "FilingArtifacts",
                column: "CalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_CompanyId_Period_Kind_VersionNumber",
                table: "FilingArtifacts",
                columns: new[] { "CompanyId", "Period", "Kind", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_FilingProfileVersionId",
                table: "FilingArtifacts",
                column: "FilingProfileVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_MonthSettlementId_Kind_InputFingerprint",
                table: "FilingArtifacts",
                columns: new[] { "MonthSettlementId", "Kind", "InputFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_PreviousArtifactId",
                table: "FilingArtifacts",
                column: "PreviousArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_ReceiptFileId",
                table: "FilingArtifacts",
                column: "ReceiptFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingArtifacts_StoredFileId",
                table: "FilingArtifacts",
                column: "StoredFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingProfileVersions_CompanyId_VersionNumber",
                table: "FilingProfileVersions",
                columns: new[] { "CompanyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilingProfileVersions_PreviousVersionId",
                table: "FilingProfileVersions",
                column: "PreviousVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilingArtifacts");

            migrationBuilder.DropTable(
                name: "FilingProfileVersions");

            migrationBuilder.DropColumn(
                name: "SellerAddress",
                table: "SourceDocuments");
        }
    }
}
