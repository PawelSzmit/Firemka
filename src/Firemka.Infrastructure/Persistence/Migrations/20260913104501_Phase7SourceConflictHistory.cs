using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7SourceConflictHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceDocumentConflicts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConflictingSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceDocumentConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceDocumentConflicts_SourceDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "SourceDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceDocumentConflicts_StoredFiles_StoredFileId",
                        column: x => x.StoredFileId,
                        principalTable: "StoredFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocumentConflicts_SourceDocumentId_ConflictingSha256",
                table: "SourceDocumentConflicts",
                columns: new[] { "SourceDocumentId", "ConflictingSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocumentConflicts_SourceDocumentId_DetectedAtUtc",
                table: "SourceDocumentConflicts",
                columns: new[] { "SourceDocumentId", "DetectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceDocumentConflicts_StoredFileId",
                table: "SourceDocumentConflicts",
                column: "StoredFileId",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "SourceDocumentConflicts"
                    ("Id", "SourceDocumentId", "StoredFileId", "ConflictingSha256",
                     "DetectedAtUtc", "ResolvedAtUtc", "Resolution")
                SELECT gen_random_uuid(), "Id", NULL, "SourceConflictSha256",
                       COALESCE("SourceConflictDetectedAtUtc", "UpdatedAtUtc"),
                       "SourceConflictResolvedAtUtc", "SourceConflictResolution"
                FROM "SourceDocuments"
                WHERE "HasSourceConflict" = TRUE
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceDocumentConflicts");
        }
    }
}
