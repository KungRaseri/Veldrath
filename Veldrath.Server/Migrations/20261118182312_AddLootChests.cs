using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLootChests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LootChests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChestId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ZoneInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LootTableRefs = table.Column<string>(type: "text", nullable: false),
                    MergeStrategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsOpened = table.Column<bool>(type: "boolean", nullable: false),
                    SpawnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpenedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootChests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootChests_ZoneInstances_ZoneInstanceId",
                        column: x => x.ZoneInstanceId,
                        principalTable: "ZoneInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LootChests_ChestId",
                table: "LootChests",
                column: "ChestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LootChests_ReopenAt",
                table: "LootChests",
                column: "ReopenAt");

            migrationBuilder.CreateIndex(
                name: "IX_LootChests_ZoneId_Kind_IsOpened",
                table: "LootChests",
                columns: new[] { "ZoneId", "Kind", "IsOpened" });

            migrationBuilder.CreateIndex(
                name: "IX_LootChests_ZoneId_LocationSlug",
                table: "LootChests",
                columns: new[] { "ZoneId", "LocationSlug" });

            migrationBuilder.CreateIndex(
                name: "IX_LootChests_ZoneInstanceId",
                table: "LootChests",
                column: "ZoneInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LootChests");
        }
    }
}
