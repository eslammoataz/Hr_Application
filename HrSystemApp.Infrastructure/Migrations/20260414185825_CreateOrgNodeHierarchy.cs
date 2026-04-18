using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateOrgNodeHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HierarchyLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ParentLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HierarchyLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HierarchyLevels_HierarchyLevels_ParentLevelId",
                        column: x => x.ParentLevelId,
                        principalTable: "HierarchyLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgNodes_HierarchyLevels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "HierarchyLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrgNodes_OrgNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrgNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgNodeAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgNodeAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgNodeAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrgNodeAssignments_OrgNodes_OrgNodeId",
                        column: x => x.OrgNodeId,
                        principalTable: "OrgNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyLevels_ParentLevelId",
                table: "HierarchyLevels",
                column: "ParentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyLevels_SortOrder",
                table: "HierarchyLevels",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodeAssignments_EmployeeId",
                table: "OrgNodeAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodeAssignments_OrgNodeId_EmployeeId",
                table: "OrgNodeAssignments",
                columns: new[] { "OrgNodeId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodes_EntityId_EntityType",
                table: "OrgNodes",
                columns: new[] { "EntityId", "EntityType" },
                unique: true,
                filter: "\"EntityId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodes_LevelId",
                table: "OrgNodes",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgNodes_ParentId",
                table: "OrgNodes",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgNodeAssignments");

            migrationBuilder.DropTable(
                name: "OrgNodes");

            migrationBuilder.DropTable(
                name: "HierarchyLevels");
        }
    }
}
