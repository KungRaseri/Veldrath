using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealmEngine.Data.Migrations.GameDb
{
    /// <inheritdoc />
    public partial class InitialGameDbSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallOfFameEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    ClassName = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PlayTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalEnemiesDefeated = table.Column<int>(type: "integer", nullable: false),
                    QuestsCompleted = table.Column<int>(type: "integer", nullable: false),
                    DeathCount = table.Column<int>(type: "integer", nullable: false),
                    DeathReason = table.Column<string>(type: "text", nullable: false),
                    DeathLocation = table.Column<string>(type: "text", nullable: false),
                    DeathDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AchievementsUnlocked = table.Column<int>(type: "integer", nullable: false),
                    IsPermadeath = table.Column<bool>(type: "boolean", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "text", nullable: false),
                    FameScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallOfFameEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaveGameId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemRef = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaveGames",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PlayerName = table.Column<string>(type: "text", nullable: false),
                    SlotIndex = table.Column<int>(type: "integer", nullable: false),
                    SaveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaveGames", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallOfFameEntries_FameScore",
                table: "HallOfFameEntries",
                column: "FameScore");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_SaveGameId_CharacterName",
                table: "InventoryRecords",
                columns: new[] { "SaveGameId", "CharacterName" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_SaveGameId_CharacterName_ItemRef",
                table: "InventoryRecords",
                columns: new[] { "SaveGameId", "CharacterName", "ItemRef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaveGames_PlayerName",
                table: "SaveGames",
                column: "PlayerName");

            migrationBuilder.CreateIndex(
                name: "IX_SaveGames_SlotIndex",
                table: "SaveGames",
                column: "SlotIndex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallOfFameEntries");

            migrationBuilder.DropTable(
                name: "InventoryRecords");

            migrationBuilder.DropTable(
                name: "SaveGames");
        }
    }
}
