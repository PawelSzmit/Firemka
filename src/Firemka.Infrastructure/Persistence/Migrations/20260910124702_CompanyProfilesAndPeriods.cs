using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompanyProfilesAndPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ServiceDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BusinessStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counterparty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counterparty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Counterparty_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnergyRatePeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossPricePerKwh = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyRatePeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyRatePeriod_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionRatePeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    NetMonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionRatePeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionRatePeriod_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxYear",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TaxationForm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxYear", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxYear_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VatProfilePeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatProfilePeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VatProfilePeriod_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleProfilePeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Arrangement = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MixedUse = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleProfilePeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleProfilePeriod_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZusProfilePeriod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFromMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Profile = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZusProfilePeriod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZusProfilePeriod_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_OwnerUserId",
                table: "Companies",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counterparty_CompanyId",
                table: "Counterparty",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnergyRatePeriod_CompanyId_ValidFromMonth",
                table: "EnergyRatePeriod",
                columns: new[] { "CompanyId", "ValidFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionRatePeriod_CompanyId_ValidFromMonth",
                table: "SubscriptionRatePeriod",
                columns: new[] { "CompanyId", "ValidFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxYear_CompanyId_Year",
                table: "TaxYear",
                columns: new[] { "CompanyId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VatProfilePeriod_CompanyId_ValidFromMonth",
                table: "VatProfilePeriod",
                columns: new[] { "CompanyId", "ValidFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleProfilePeriod_CompanyId_ValidFromMonth",
                table: "VehicleProfilePeriod",
                columns: new[] { "CompanyId", "ValidFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZusProfilePeriod_CompanyId_ValidFromMonth",
                table: "ZusProfilePeriod",
                columns: new[] { "CompanyId", "ValidFromMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Counterparty");

            migrationBuilder.DropTable(
                name: "EnergyRatePeriod");

            migrationBuilder.DropTable(
                name: "SubscriptionRatePeriod");

            migrationBuilder.DropTable(
                name: "TaxYear");

            migrationBuilder.DropTable(
                name: "VatProfilePeriod");

            migrationBuilder.DropTable(
                name: "VehicleProfilePeriod");

            migrationBuilder.DropTable(
                name: "ZusProfilePeriod");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
