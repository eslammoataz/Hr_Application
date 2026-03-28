using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileUpdateRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfileUpdateRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EmployeeComment = table.Column<string>(type: "text", nullable: true),
                    HrNote = table.Column<string>(type: "text", nullable: true),
                    HandledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HandledByHrId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileUpdateRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileUpdateRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileUpdateRequests_Employees_HandledByHrId",
                        column: x => x.HandledByHrId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileUpdateRequests_EmployeeId",
                table: "ProfileUpdateRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileUpdateRequests_HandledByHrId",
                table: "ProfileUpdateRequests",
                column: "HandledByHrId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileUpdateRequests");
        }
    }
}
