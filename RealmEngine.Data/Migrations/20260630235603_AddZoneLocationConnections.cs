using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneLocationConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneLocationConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ToLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToRegionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConnectionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsTraversable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneLocationConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocationConnections_FromLocationSlug_ToLocationSlug",
                table: "ZoneLocationConnections",
                columns: new[] { "FromLocationSlug", "ToLocationSlug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocationConnections_FromLocationSlug_ToRegionId",
                table: "ZoneLocationConnections",
                columns: new[] { "FromLocationSlug", "ToRegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocationConnections_FromLocationSlug_ToZoneId",
                table: "ZoneLocationConnections",
                columns: new[] { "FromLocationSlug", "ToZoneId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneLocationConnections");
        }
    }
}
