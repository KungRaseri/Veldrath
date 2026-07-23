using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneLocationTileCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TileX",
                table: "ZoneLocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TileY",
                table: "ZoneLocations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TileX",
                table: "ZoneLocations");

            migrationBuilder.DropColumn(
                name: "TileY",
                table: "ZoneLocations");
        }
    }
}
