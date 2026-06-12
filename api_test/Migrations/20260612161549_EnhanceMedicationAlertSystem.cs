using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceMedicationAlertSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdvanceReminderMinutes",
                table: "UserMedications",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MedicationSchedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdvanceReminderSent",
                table: "MedicationSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DueReminderSent",
                table: "MedicationSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MissedAt",
                table: "MedicationSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SkippedAt",
                table: "MedicationSchedules",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvanceReminderMinutes",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "AdvanceReminderSent",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "DueReminderSent",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "MissedAt",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "MedicationSchedules");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MedicationSchedules",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
