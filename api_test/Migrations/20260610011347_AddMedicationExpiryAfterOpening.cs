using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicationExpiryAfterOpening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfterOpeningDurationUnit",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "days");

            migrationBuilder.AddColumn<int>(
                name: "AfterOpeningDurationValue",
                table: "UserMedications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AfterOpeningExpiryDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterOpeningSource",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveExpiryDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpiryReason",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpened",
                table: "UserMedications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedDate",
                table: "UserMedications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfterOpeningNote",
                table: "Medications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultAfterOpeningUnit",
                table: "Medications",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "days");

            migrationBuilder.AddColumn<int>(
                name: "DefaultAfterOpeningValue",
                table: "Medications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOpeningTracking",
                table: "Medications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE UserMedications
                SET
                    EffectiveExpiryDate = ExpiryDate,
                    ExpiryReason = 'PACKAGE_EXPIRY',
                    AfterOpeningDurationUnit = COALESCE(AfterOpeningDurationUnit, 'days')
                WHERE EffectiveExpiryDate IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE Medications
                SET RequiresOpeningTracking = 1
                WHERE UPPER(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE(Dosage_Form, ''))), '-', '_'), ' ', '_')) IN
                (
                    'OPHTHALMIC_SOLUTION',
                    'ORAL_DROP',
                    'ORAL_DROPS',
                    'SYRUP',
                    'SUSPENSION',
                    'ORAL_SOLUTION',
                    'GEL',
                    'EMULGEL'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterOpeningDurationUnit",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "AfterOpeningDurationValue",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "AfterOpeningExpiryDate",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "AfterOpeningSource",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "EffectiveExpiryDate",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "ExpiryReason",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "IsOpened",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "OpenedDate",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "AfterOpeningNote",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "DefaultAfterOpeningUnit",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "DefaultAfterOpeningValue",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "RequiresOpeningTracking",
                table: "Medications");
        }
    }
}
