using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class FixDeleteOnAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Users_UserId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts",
                column: "UserMedicationId",
                principalTable: "UserMedications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Users_UserId",
                table: "Alerts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Users_UserId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_UserMedications_UserMedicationId",
                table: "Alerts",
                column: "UserMedicationId",
                principalTable: "UserMedications",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Users_UserId",
                table: "Alerts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
