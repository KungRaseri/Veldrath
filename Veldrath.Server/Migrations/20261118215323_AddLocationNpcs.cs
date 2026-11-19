using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationNpcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationNpcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArchetypeSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationNpcs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationNpcs_NpcId",
                table: "LocationNpcs",
                column: "NpcId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationNpcs_ZoneId_LocationSlug",
                table: "LocationNpcs",
                columns: new[] { "ZoneId", "LocationSlug" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationNpcs_ZoneId_LocationSlug_ArchetypeSlug",
                table: "LocationNpcs",
                columns: new[] { "ZoneId", "LocationSlug", "ArchetypeSlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationNpcs");
        }
    }
}
