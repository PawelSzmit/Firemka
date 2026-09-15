using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Firemka.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesInvoicesAndOutgoingKsef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesAutomationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAutomationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesAutomationSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ServiceMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    ServicePeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ServicePeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledIssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SubscriptionRatePeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    HasManualAmountOverride = table.Column<bool>(type: "boolean", nullable: false),
                    PendingSubscriptionRatePeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    PendingSubscriptionNetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PendingSubscriptionVatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    DraftRevision = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IssuedAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    OutgoingXml = table.Column<string>(type: "text", nullable: true),
                    SessionReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubmissionReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    KsefNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpoXml = table.Column<string>(type: "text", nullable: true),
                    ReturnedKsefXml = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    LastStatusCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAutomationSettings_CompanyId",
                table: "SalesAutomationSettings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesAutomationSettings_OwnerUserId",
                table: "SalesAutomationSettings",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyId_InvoiceNumber",
                table: "SalesInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_CompanyId_ServiceMonth",
                table: "SalesInvoices",
                columns: new[] { "CompanyId", "ServiceMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_IdempotencyKey",
                table: "SalesInvoices",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_KsefNumber",
                table: "SalesInvoices",
                column: "KsefNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesAutomationSettings");

            migrationBuilder.DropTable(
                name: "SalesInvoices");
        }
    }
}
