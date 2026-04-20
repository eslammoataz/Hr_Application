using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyRoleId",
                table: "RequestWorkflowSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyRoles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyRolePermissions_CompanyRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CompanyRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeCompanyRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCompanyRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCompanyRoles_CompanyRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CompanyRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeCompanyRoles_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestWorkflowSteps_CompanyRoleId",
                table: "RequestWorkflowSteps",
                column: "CompanyRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRolePermissions_RoleId_Permission",
                table: "CompanyRolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoles_CompanyId_Name",
                table: "CompanyRoles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompanyRoles_EmployeeId_RoleId",
                table: "EmployeeCompanyRoles",
                columns: new[] { "EmployeeId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCompanyRoles_RoleId",
                table: "EmployeeCompanyRoles",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestWorkflowSteps_CompanyRoles_CompanyRoleId",
                table: "RequestWorkflowSteps",
                column: "CompanyRoleId",
                principalTable: "CompanyRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestWorkflowSteps_CompanyRoles_CompanyRoleId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropTable(
                name: "CompanyRolePermissions");

            migrationBuilder.DropTable(
                name: "EmployeeCompanyRoles");

            migrationBuilder.DropTable(
                name: "CompanyRoles");

            migrationBuilder.DropIndex(
                name: "IX_RequestWorkflowSteps_CompanyRoleId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CompanyRoleId",
                table: "RequestWorkflowSteps");
        }
    }
}
