using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrSystemApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceSystemV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "Companies",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 17, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "GraceMinutes",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "Companies",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 9, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.CreateTable(
                name: "AttendanceAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BeforeSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    AfterSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstClockInUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastClockOutUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalHours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    IsEarlyLeave = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstClockInLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastClockOutLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_AttendanceLogs_FirstClockInLogId",
                        column: x => x.FirstClockInLogId,
                        principalTable: "AttendanceLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendances_AttendanceLogs_LastClockOutLogId",
                        column: x => x.LastClockOutLogId,
                        principalTable: "AttendanceLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attendances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceReminderLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReminderType = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JobRunId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WindowKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceReminderLogs_Attendances_AttendanceId",
                        column: x => x.AttendanceId,
                        principalTable: "Attendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceReminderLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_AttendanceId",
                table: "AttendanceAdjustments",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAdjustments_EmployeeId",
                table: "AttendanceAdjustments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_AttendanceId",
                table: "AttendanceLogs",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_EmployeeId_TimestampUtc",
                table: "AttendanceLogs",
                columns: new[] { "EmployeeId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_IdempotencyKey",
                table: "AttendanceLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReminderLogs_AttendanceId_ReminderType_WindowKey",
                table: "AttendanceReminderLogs",
                columns: new[] { "AttendanceId", "ReminderType", "WindowKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReminderLogs_EmployeeId",
                table: "AttendanceReminderLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_Date_LastClockOutUtc",
                table: "Attendances",
                columns: new[] { "Date", "LastClockOutUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId_Date",
                table: "Attendances",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_FirstClockInLogId",
                table: "Attendances",
                column: "FirstClockInLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_LastClockOutLogId",
                table: "Attendances",
                column: "LastClockOutLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceAdjustments_Attendances_AttendanceId",
                table: "AttendanceAdjustments",
                column: "AttendanceId",
                principalTable: "Attendances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceLogs_Attendances_AttendanceId",
                table: "AttendanceLogs",
                column: "AttendanceId",
                principalTable: "Attendances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceLogs_Attendances_AttendanceId",
                table: "AttendanceLogs");

            migrationBuilder.DropTable(
                name: "AttendanceAdjustments");

            migrationBuilder.DropTable(
                name: "AttendanceReminderLogs");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "AttendanceLogs");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GraceMinutes",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Companies");
        }
    }
}
