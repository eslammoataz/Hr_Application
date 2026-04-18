using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMixedWorkflowStepTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrgNodeId",
                table: "RequestWorkflowSteps",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "BypassHierarchyCheck",
                table: "RequestWorkflowSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DirectEmployeeId",
                table: "RequestWorkflowSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepType",
                table: "RequestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RequestWorkflowSteps_DirectEmployeeId",
                table: "RequestWorkflowSteps",
                column: "DirectEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestWorkflowSteps_Employees_DirectEmployeeId",
                table: "RequestWorkflowSteps",
                column: "DirectEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps",
                column: "OrgNodeId",
                principalTable: "OrgNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestWorkflowSteps_Employees_DirectEmployeeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_RequestWorkflowSteps_DirectEmployeeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "BypassHierarchyCheck",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DirectEmployeeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepType",
                table: "RequestWorkflowSteps");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrgNodeId",
                table: "RequestWorkflowSteps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps",
                column: "OrgNodeId",
                principalTable: "OrgNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
