using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddGroundItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroundItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ItemRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Durability = table.Column<int>(type: "integer", nullable: true),
                    DroppedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DroppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroundItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroundItems_ZoneId_LocationSlug",
                table: "GroundItems",
                columns: new[] { "ZoneId", "LocationSlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroundItems");
        }
    }
}
