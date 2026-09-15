using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11Backups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecretSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupAccessTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ConfiguredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastManifestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastFormatVersion = table.Column<int>(type: "integer", nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupAccessTokens_OwnerUserId",
                table: "BackupAccessTokens",
                column: "OwnerUserId",
                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackupAccessTokens_SecretSha256",
                table: "BackupAccessTokens",
                column: "SecretSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupRequests_OwnerUserId",
                table: "BackupRequests",
                column: "OwnerUserId",
                unique: true,
                filter: "\"CompletedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackupStates_OwnerUserId",
                table: "BackupStates",
                column: "OwnerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupAccessTokens");

            migrationBuilder.DropTable(
                name: "BackupRequests");

            migrationBuilder.DropTable(
                name: "BackupStates");
        }
    }
}
