using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AddAdherenceRefillPrnCabinetFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefillDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastRefillQuantity",
                table: "UserMedications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDosesPerDay",
                table: "UserMedications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicationUseType",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumHoursBetweenDoses",
                table: "UserMedications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefillReminderDaysBefore",
                table: "UserMedications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionNote",
                table: "MedicationSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissedReason",
                table: "MedicationSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedicationIntakeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserMedicationId = table.Column<int>(type: "int", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuantityTaken = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationIntakeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicationIntakeLogs_UserMedications_UserMedicationId",
                        column: x => x.UserMedicationId,
                        principalTable: "UserMedications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationIntakeLogs_UserMedicationId",
                table: "MedicationIntakeLogs",
                column: "UserMedicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicationIntakeLogs");

            migrationBuilder.DropColumn(
                name: "LastRefillDate",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "LastRefillQuantity",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "MaxDosesPerDay",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "MedicationUseType",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "MinimumHoursBetweenDoses",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "RefillReminderDaysBefore",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "ActionNote",
                table: "MedicationSchedules");

            migrationBuilder.DropColumn(
                name: "MissedReason",
                table: "MedicationSchedules");
        }
    }
}
