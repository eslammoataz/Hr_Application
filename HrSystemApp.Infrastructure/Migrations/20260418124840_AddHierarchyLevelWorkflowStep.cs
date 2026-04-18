using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchyLevelWorkflowStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelsUp",
                table: "RequestWorkflowSteps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartFromLevel",
                table: "RequestWorkflowSteps",
                type: "integer",
                nullable: true);

            // (Seed) Add one example HierarchyLevel definition for the demo company if conditions are met.
            // Safe: only inserts when exactly one company exists and that company has no existing Leave definition.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    demo_company_id uuid;
    demo_def_id uuid := gen_random_uuid();
    demo_step_id uuid := gen_random_uuid();
BEGIN
    IF (SELECT COUNT(*) FROM ""Companies"") = 1 THEN
        SELECT ""Id"" INTO demo_company_id FROM ""Companies"" LIMIT 1;

        IF NOT EXISTS (
            SELECT 1 FROM ""RequestDefinitions""
            WHERE ""CompanyId"" = demo_company_id AND ""RequestType"" = 0
        ) THEN
            INSERT INTO ""RequestDefinitions"" (""Id"", ""CompanyId"", ""RequestType"", ""IsActive"", ""FormSchemaJson"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"", ""CreatedById"", ""UpdatedById"")
            VALUES (demo_def_id, demo_company_id, 0, TRUE, NULL, NOW(), NULL, FALSE, NULL, NULL);

            INSERT INTO ""RequestWorkflowSteps"" (""Id"", ""RequestDefinitionId"", ""StepType"", ""OrgNodeId"", ""BypassHierarchyCheck"", ""DirectEmployeeId"", ""StartFromLevel"", ""LevelsUp"", ""SortOrder"", ""CreatedAt"", ""IsDeleted"", ""CreatedById"", ""UpdatedAt"", ""UpdatedById"")
            VALUES (demo_step_id, demo_def_id, 2, NULL, FALSE, NULL, 1, 3, 1, NOW(), FALSE, NULL, NULL, NULL);
        END IF;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""RequestWorkflowSteps""
WHERE ""StartFromLevel"" IS NOT NULL OR ""LevelsUp"" IS NOT NULL;
");

            migrationBuilder.DropColumn(
                name: "LevelsUp",
                table: "RequestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StartFromLevel",
                table: "RequestWorkflowSteps");
        }
    }
}
