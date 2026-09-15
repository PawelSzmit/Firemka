using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoredFileProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $firemka$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "StoredFiles") THEN
                        RAISE EXCEPTION 'StoredFileProvenance requires an empty StoredFiles table.';
                    END IF;
                END
                $firemka$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "StoredFiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RecordId",
                table: "StoredFiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RecordType",
                table: "StoredFiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RecordVersion",
                table: "StoredFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                ALTER TABLE "StoredFiles"
                    ALTER COLUMN "Origin" DROP DEFAULT,
                    ALTER COLUMN "RecordId" DROP DEFAULT,
                    ALTER COLUMN "RecordType" DROP DEFAULT,
                    ALTER COLUMN "RecordVersion" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_RecordType_RecordId_RecordVersion",
                table: "StoredFiles",
                columns: new[] { "RecordType", "RecordId", "RecordVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_RecordType_RecordId_RecordVersion",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "RecordId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "RecordType",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "RecordVersion",
                table: "StoredFiles");
        }
    }
}
