using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeNoAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts",
                column: "UserMedicationId",
                principalTable: "UserMedications",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts",
                column: "UserMedicationId",
                principalTable: "UserMedications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
