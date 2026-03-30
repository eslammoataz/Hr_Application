using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedRequestModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AssetCode",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AssetStatus",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "AssetType",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "EndDateTime",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "IsHourly",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "LeaveSubType",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PerDiem",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "StartDateTime",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "TotalEstimate",
                table: "Requests");

            migrationBuilder.AddColumn<string>(
                name: "Data",
                table: "Requests",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "FormSchemaJson",
                table: "RequestDefinitions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Data",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "FormSchemaJson",
                table: "RequestDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "Requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetCode",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetStatus",
                table: "Requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssetType",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Requests",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Duration",
                table: "Requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDateTime",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHourly",
                table: "Requests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeaveSubType",
                table: "Requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PerDiem",
                table: "Requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateTime",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEstimate",
                table: "Requests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Requests_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");
        }
    }
}
