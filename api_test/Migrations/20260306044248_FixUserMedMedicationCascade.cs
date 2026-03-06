using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class FixUserMedMedicationCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
       name: "FK_UserMedications_Medications_MedId",
       table: "UserMedications");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications",
                column: "MedId",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
        name: "FK_UserMedications_Medications_MedId",
        table: "UserMedications");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications",
                column: "MedId",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
