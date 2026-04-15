using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrgNodeBasedApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Requests_Employees_CurrentApproverId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_CurrentApproverId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequiredRole",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CurrentApproverId",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PlannedChainJson",
                table: "Requests");

            migrationBuilder.AddColumn<Guid>(
                name: "OrgNodeId",
                table: "RequestWorkflowSteps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "CurrentStepOrder",
                table: "Requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlannedStepsJson",
                table: "Requests",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestWorkflowSteps_OrgNodeId",
                table: "RequestWorkflowSteps",
                column: "OrgNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps",
                column: "OrgNodeId",
                principalTable: "OrgNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestWorkflowSteps_OrgNodes_OrgNodeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_RequestWorkflowSteps_OrgNodeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "OrgNodeId",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CurrentStepOrder",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PlannedStepsJson",
                table: "Requests");

            migrationBuilder.AddColumn<int>(
                name: "RequiredRole",
                table: "RequestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverId",
                table: "Requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlannedChainJson",
                table: "Requests",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CurrentApproverId",
                table: "Requests",
                column: "CurrentApproverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Requests_Employees_CurrentApproverId",
                table: "Requests",
                column: "CurrentApproverId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
