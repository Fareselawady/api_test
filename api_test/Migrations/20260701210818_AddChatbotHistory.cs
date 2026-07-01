using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_test.Migrations
{
    /// <inheritdoc />
    public partial class AddChatbotHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AiConversationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatConversationId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetectedLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Intent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MatchedDrugsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SafetyFlagsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatConversations_ChatConversationId",
                        column: x => x.ChatConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId_AiConversationId",
                table: "ChatConversations",
                columns: new[] { "UserId", "AiConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId_IsDeleted_UpdatedAt",
                table: "ChatConversations",
                columns: new[] { "UserId", "IsDeleted", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatConversationId_CreatedAt",
                table: "ChatMessages",
                columns: new[] { "ChatConversationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatConversations");
        }
    }
}
