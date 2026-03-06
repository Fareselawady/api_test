using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class FixMedIngredientLinkCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
       name: "FK_Med_Link",
       table: "Med_Ingredients_Link");

            migrationBuilder.AddForeignKey(
                name: "FK_Med_Link",
                table: "Med_Ingredients_Link",
                column: "Med_id",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
       name: "FK_Med_Link",
       table: "Med_Ingredients_Link");

            migrationBuilder.AddForeignKey(
                name: "FK_Med_Link",
                table: "Med_Ingredients_Link",
                column: "Med_id",
                principalTable: "Medications",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
