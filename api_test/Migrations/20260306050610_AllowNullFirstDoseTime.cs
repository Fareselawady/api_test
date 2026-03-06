using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullFirstDoseTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
      name: "FirstDoseTime",
      table: "UserMedications",
      nullable: true,
      oldClrType: typeof(TimeSpan),
      oldNullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
        name: "FirstDoseTime",
        table: "UserMedications",
        nullable: false,
        oldClrType: typeof(TimeSpan),
        oldNullable: true);
        }
    }
}
