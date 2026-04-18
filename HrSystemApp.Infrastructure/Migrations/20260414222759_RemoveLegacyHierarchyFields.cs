using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyHierarchyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrgNodes_HierarchyLevels_LevelId",
                table: "OrgNodes");

            migrationBuilder.DropTable(
                name: "HierarchyLevels");

            migrationBuilder.DropIndex(
                name: "IX_OrgNodes_EntityId_EntityType",
                table: "OrgNodes");

            migrationBuilder.DropIndex(
                name: "IX_OrgNodes_LevelId",
                table: "OrgNodes");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "OrgNodes");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "OrgNodes");

            migrationBuilder.DropColumn(
                name: "LevelId",
                table: "OrgNodes");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "OrgNodes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "OrgNodes");

            migrationBuilder.AddColumn<Guid>(
                name: "EntityId",
                table: "OrgNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntityType",
                table: "OrgNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LevelId",
                table: "OrgNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HierarchyLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "IX_HierarchyLevels_ParentLevelId",
                table: "HierarchyLevels",
                column: "ParentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyLevels_SortOrder",
                table: "HierarchyLevels",
                column: "SortOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_OrgNodes_HierarchyLevels_LevelId",
                table: "OrgNodes",
                column: "LevelId",
                principalTable: "HierarchyLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
