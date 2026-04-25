using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCapturedSchemaJsonToRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "RequestDefinitions");

            migrationBuilder.RenameColumn(
                name: "Data",
                table: "Requests",
                newName: "DynamicDataJson");

            migrationBuilder.AddColumn<string>(
                name: "CapturedSchemaJson",
                table: "Requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestNumber",
                table: "Requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestTypeId",
                table: "Requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestTypeId1",
                table: "Requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                table: "Requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestTypeId",
                table: "RequestDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestTypeId1",
                table: "RequestDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystemType = table.Column<bool>(type: "boolean", nullable: false),
                    IsCustomType = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormSchemaJson = table.Column<string>(type: "text", nullable: true),
                    AllowExtraFields = table.Column<bool>(type: "boolean", nullable: false),
                    RequestNumberPattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DefaultSlaDays = table.Column<int>(type: "integer", nullable: true),
                    DisplayNameLocalizationsJson = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestTypes_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_DueDate",
                table: "Requests",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestTypeId",
                table: "Requests",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestTypeId1",
                table: "Requests",
                column: "RequestTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_SlaBreachedAt",
                table: "Requests",
                column: "SlaBreachedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDefinitions_RequestTypeId",
                table: "RequestDefinitions",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestDefinitions_RequestTypeId1",
                table: "RequestDefinitions",
                column: "RequestTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTypes_CompanyId",
                table: "RequestTypes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTypes_KeyName_CompanyId",
                table: "RequestTypes",
                columns: new[] { "KeyName", "CompanyId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestDefinitions_RequestTypes_RequestTypeId",
                table: "RequestDefinitions",
                column: "RequestTypeId",
                principalTable: "RequestTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestDefinitions_RequestTypes_RequestTypeId1",
                table: "RequestDefinitions",
                column: "RequestTypeId1",
                principalTable: "RequestTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_RequestTypes_RequestTypeId",
                table: "Requests",
                column: "RequestTypeId",
                principalTable: "RequestTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_RequestTypes_RequestTypeId1",
                table: "Requests",
                column: "RequestTypeId1",
                principalTable: "RequestTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestDefinitions_RequestTypes_RequestTypeId",
                table: "RequestDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestDefinitions_RequestTypes_RequestTypeId1",
                table: "RequestDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_RequestTypes_RequestTypeId",
                table: "Requests");

            migrationBuilder.DropForeignKey(
                name: "FK_Requests_RequestTypes_RequestTypeId1",
                table: "Requests");

            migrationBuilder.DropTable(
                name: "RequestTypes");

            migrationBuilder.DropIndex(
                name: "IX_Requests_DueDate",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RequestTypeId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RequestTypeId1",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_SlaBreachedAt",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_RequestDefinitions_RequestTypeId",
                table: "RequestDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_RequestDefinitions_RequestTypeId1",
                table: "RequestDefinitions");

            migrationBuilder.DropColumn(
                name: "CapturedSchemaJson",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestNumber",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestTypeId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestTypeId1",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestTypeId",
                table: "RequestDefinitions");

            migrationBuilder.DropColumn(
                name: "RequestTypeId1",
                table: "RequestDefinitions");

            migrationBuilder.RenameColumn(
                name: "DynamicDataJson",
                table: "Requests",
                newName: "Data");

            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "Requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequestType",
                table: "RequestDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
