using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations;

public partial class AddCurrentStepApproverIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentStepApproverIds",
            table: "Requests",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Requests_CurrentStepApproverIds",
            table: "Requests",
            column: "CurrentStepApproverIds");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Requests_CurrentStepApproverIds",
            table: "Requests");

        migrationBuilder.DropColumn(
            name: "CurrentStepApproverIds",
            table: "Requests");
    }
}