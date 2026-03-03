using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class editsometabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            



            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "IntervalHours",
                table: "UserMedications",
                type: "float",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpiryDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserMedId",
                table: "MedicationSchedules",
                newName: "UserMedicationId");

            migrationBuilder.RenameIndex(
                name: "IX_MedicationSchedules_UserMedId",
                table: "MedicationSchedules",
                newName: "IX_MedicationSchedules_UserMedicationId");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                table: "UserMedications",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IntervalHours",
                table: "UserMedications",
                type: "int",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExpiryDate",
                table: "UserMedications",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                table: "UserMedications",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "MedicationSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnoozedUntil",
                table: "MedicationSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TakenAt",
                table: "MedicationSchedules",
                type: "datetime2",
                nullable: true);

        }
    }
}
