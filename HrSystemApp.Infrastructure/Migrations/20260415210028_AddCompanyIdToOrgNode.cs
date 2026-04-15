using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToOrgNode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "OrgNodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodes_CompanyId",
                table: "OrgNodes",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrgNodes_CompanyId",
                table: "OrgNodes");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "OrgNodes");
        }
    }
}
