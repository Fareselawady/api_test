using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class EditFullDeleteCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_MedicationSchedules_MedicationScheduleId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_MedicationSchedules_MedicationScheduleId",
                table: "Alerts",
                column: "MedicationScheduleId",
                principalTable: "MedicationSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_MedicationSchedules_MedicationScheduleId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_MedicationSchedules_MedicationScheduleId",
                table: "Alerts",
                column: "MedicationScheduleId",
                principalTable: "MedicationSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
