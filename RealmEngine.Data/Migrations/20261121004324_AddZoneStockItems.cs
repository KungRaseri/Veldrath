using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneStockItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneStockItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneStockItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneStockItems_ZoneId_ItemSlug",
                table: "ZoneStockItems",
                columns: new[] { "ZoneId", "ItemSlug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneStockItems");
        }
    }
}
