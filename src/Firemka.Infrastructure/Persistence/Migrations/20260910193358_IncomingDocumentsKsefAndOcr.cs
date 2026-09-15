using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncomingDocumentsKsefAndOcr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "SourceDocuments" LIMIT 1) THEN
                        RAISE EXCEPTION 'IncomingDocumentsKsefAndOcr requires an empty SourceDocuments table so ownership and origin are never invented.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "SourceDocuments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataRevisionNumber",
                table: "SourceDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtractionConfidence",
                table: "SourceDocuments",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                table: "SourceDocuments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasSourceConflict",
                table: "SourceDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "SourceDocuments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IssueDate",
                table: "SourceDocuments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KsefNumber",
                table: "SourceDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "KsefPermanentStorageDateUtc",
                table: "SourceDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "SourceDocuments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "SourceDocuments",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SellerName",
                table: "SourceDocuments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerTaxId",
                table: "SourceDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceConflictDetectedAtUtc",
                table: "SourceDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceFileId",
                table: "SourceDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSha256",
                table: "SourceDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnrelatedReason",
                table: "SourceDocuments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentExtractionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Engine = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    FieldConfidencesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentExtractionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentExtractionAttempts_SourceDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "SourceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentExtractionAttempts_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KsefSyncCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InitialFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Cursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KsefSyncCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KsefSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedFromCursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FinishedAtCursor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImportedCount = table.Column<int>(type: "integer", nullable: false),
                    UnchangedCount = table.Column<int>(type: "integer", nullable: false),
                    ConflictCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KsefSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocuments_OwnerUserId_KsefNumber",
                table: "SourceDocuments",
                columns: new[] { "OwnerUserId", "KsefNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocuments_OwnerUserId_SourceSha256",
                table: "SourceDocuments",
                columns: new[] { "OwnerUserId", "SourceSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocuments_SourceFileId",
                table: "SourceDocuments",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExtractionAttempts_SourceDocumentId_AttemptNumber",
                table: "DocumentExtractionAttempts",
                columns: new[] { "SourceDocumentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentExtractionAttempts_StoredFileId",
                table: "DocumentExtractionAttempts",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_KsefSyncCheckpoints_OwnerUserId_Environment",
                table: "KsefSyncCheckpoints",
                columns: new[] { "OwnerUserId", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KsefSyncRuns_OwnerUserId_StartedAtUtc",
                table: "KsefSyncRuns",
                columns: new[] { "OwnerUserId", "StartedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_SourceDocuments_StoredFiles_SourceFileId",
                table: "SourceDocuments",
                column: "SourceFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SourceDocuments_StoredFiles_SourceFileId",
                table: "SourceDocuments");

            migrationBuilder.DropTable(
                name: "DocumentExtractionAttempts");

            migrationBuilder.DropTable(
                name: "KsefSyncCheckpoints");

            migrationBuilder.DropTable(
                name: "KsefSyncRuns");

            migrationBuilder.DropIndex(
                name: "IX_SourceDocuments_OwnerUserId_KsefNumber",
                table: "SourceDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SourceDocuments_OwnerUserId_SourceSha256",
                table: "SourceDocuments");

            migrationBuilder.DropIndex(
                name: "IX_SourceDocuments_SourceFileId",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "DataRevisionNumber",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractionConfidence",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "HasSourceConflict",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "KsefNumber",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "KsefPermanentStorageDateUtc",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SellerName",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SellerTaxId",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceConflictDetectedAtUtc",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceFileId",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceSha256",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "UnrelatedReason",
                table: "SourceDocuments");
        }
    }
}
