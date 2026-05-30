using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneralMedicationQuantityTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentQuantity",
                table: "UserMedications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DosageForm",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoseQuantity",
                table: "UserMedications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialQuantity",
                table: "UserMedications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnit",
                table: "UserMedications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE um
                SET
                    um.DosageForm = m.Dosage_Form,
                    um.QuantityUnit =
                        CASE UPPER(REPLACE(REPLACE(LTRIM(RTRIM(COALESCE(m.Dosage_Form, ''))), '-', '_'), ' ', '_'))
                            WHEN 'TAB' THEN 'tablet'
                            WHEN 'TAB_EC' THEN 'tablet'
                            WHEN 'TAB_SR' THEN 'tablet'
                            WHEN 'TAB_EFF' THEN 'tablet'
                            WHEN 'TAB_CHEW' THEN 'tablet'
                            WHEN 'TAB_MR' THEN 'tablet'
                            WHEN 'TAB_PR' THEN 'tablet'
                            WHEN 'TAB_ER' THEN 'tablet'
                            WHEN 'TAB_SC' THEN 'tablet'
                            WHEN 'TAB_DISPERSIBLE' THEN 'tablet'
                            WHEN 'CAPSULE' THEN 'capsule'
                            WHEN 'CAPSULE_SG' THEN 'capsule'
                            WHEN 'CAPSULE_SR' THEN 'capsule'
                            WHEN 'CAPSULE_EC' THEN 'capsule'
                            WHEN 'CAPSULE_SG_EC' THEN 'capsule'
                            WHEN 'SYRUP' THEN 'ml'
                            WHEN 'SUSPENSION' THEN 'ml'
                            WHEN 'ORAL_SOLUTION' THEN 'ml'
                            WHEN 'ORAL_DROP' THEN 'drops'
                            WHEN 'ORAL_DROPS' THEN 'drops'
                            WHEN 'GEL' THEN 'g'
                            WHEN 'EMULGEL' THEN 'g'
                            WHEN 'OINTMENT' THEN 'g'
                            WHEN 'CREAM' THEN 'g'
                            WHEN 'TOPICAL_PATCH' THEN 'patch'
                            WHEN 'AMPOULE' THEN 'ampoule'
                            WHEN 'VIAL_POWDER' THEN 'vial'
                            WHEN 'VIAL' THEN 'vial'
                            WHEN 'SUPPOSITORY' THEN 'suppository'
                            WHEN 'SUPPOSITORIES' THEN 'suppository'
                            WHEN 'SACHET' THEN 'sachet'
                            WHEN 'SACHETS' THEN 'sachet'
                            WHEN 'EC_GRANULES_SACHETS' THEN 'sachet'
                            ELSE 'unit'
                        END,
                    um.InitialQuantity = CAST(um.InitialPillCount AS decimal(18, 2)),
                    um.CurrentQuantity = CAST(um.CurrentPillCount AS decimal(18, 2)),
                    um.DoseQuantity = CAST(um.PillsPerDose AS decimal(18, 2))
                FROM UserMedications um
                LEFT JOIN Medications m ON m.ID = um.MedId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentQuantity",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "DosageForm",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "DoseQuantity",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "InitialQuantity",
                table: "UserMedications");

            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                table: "UserMedications");
        }
    }
}
