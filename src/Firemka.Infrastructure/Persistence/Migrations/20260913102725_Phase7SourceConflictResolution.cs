using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7SourceConflictResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceConflictResolution",
                table: "SourceDocuments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceConflictResolvedAtUtc",
                table: "SourceDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceConflictSha256",
                table: "SourceDocuments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceConflictResolution",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceConflictResolvedAtUtc",
                table: "SourceDocuments");

            migrationBuilder.DropColumn(
                name: "SourceConflictSha256",
                table: "SourceDocuments");
        }
    }
}
