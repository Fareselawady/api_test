using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomUserMedications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications");

            migrationBuilder.AlterColumn<int>(
                name: "MedId",
                table: "UserMedications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomMedication",
                table: "UserMedications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MedicationName",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE um
                SET um.MedicationName = COALESCE(m.Trade_name, '')
                FROM UserMedications um
                LEFT JOIN Medications m ON m.ID = um.MedId
                WHERE um.MedId IS NOT NULL;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications",
                column: "MedId",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "IsCustomMedication",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "MedicationName",
                table: "UserMedications");

            migrationBuilder.Sql(@"
                DELETE FROM UserMedications
                WHERE MedId IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "MedId",
                table: "UserMedications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserMedications_Medications_MedId",
                table: "UserMedications",
                column: "MedId",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
