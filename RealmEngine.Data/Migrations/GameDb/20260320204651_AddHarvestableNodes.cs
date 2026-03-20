using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations.GameDb
{
    /// <inheritdoc />
    public partial class AddHarvestableNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HarvestableNodes",
                columns: table => new
                {
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    MaterialTier = table.Column<string>(type: "text", nullable: false),
                    CurrentHealth = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    LastHarvestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimesHarvested = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BiomeType = table.Column<string>(type: "text", nullable: false),
                    LootTableRef = table.Column<string>(type: "text", nullable: false),
                    MinToolTier = table.Column<int>(type: "integer", nullable: false),
                    BaseYield = table.Column<int>(type: "integer", nullable: false),
                    IsRichNode = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestableNodes", x => x.NodeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HarvestableNodes_LocationId",
                table: "HarvestableNodes",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HarvestableNodes");
        }
    }
}
