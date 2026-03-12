using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class fixignorefromtabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "SnoozedUntil",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "TakenAt",
                table: "MedicationSchedules");
        }
    }
}
