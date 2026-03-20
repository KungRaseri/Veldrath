using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Migrations.Application
{
    /// <inheritdoc />
    public partial class RemoveGameEntitiesFromApplicationDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallOfFameEntries");

            migrationBuilder.DropTable(
                name: "SaveGames");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallOfFameEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AchievementsUnlocked = table.Column<int>(type: "integer", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    ClassName = table.Column<string>(type: "text", nullable: false),
                    DeathCount = table.Column<int>(type: "integer", nullable: false),
                    DeathDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeathLocation = table.Column<string>(type: "text", nullable: false),
                    DeathReason = table.Column<string>(type: "text", nullable: false),
                    DifficultyLevel = table.Column<string>(type: "text", nullable: false),
                    FameScore = table.Column<int>(type: "integer", nullable: false),
                    IsPermadeath = table.Column<bool>(type: "boolean", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PlayTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    QuestsCompleted = table.Column<int>(type: "integer", nullable: false),
                    TotalEnemiesDefeated = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallOfFameEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaveGames",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    PlayerName = table.Column<string>(type: "text", nullable: false),
                    SaveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SlotIndex = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_SaveGames_PlayerName",
                table: "SaveGames",
                column: "PlayerName");
        }
    }
}
