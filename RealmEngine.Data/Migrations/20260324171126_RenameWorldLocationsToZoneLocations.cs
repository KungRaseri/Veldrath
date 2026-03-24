using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameWorldLocationsToZoneLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldLocations");

            migrationBuilder.CreateTable(
                name: "ZoneLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LocationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocations_TypeKey",
                table: "ZoneLocations",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocations_TypeKey_Slug",
                table: "ZoneLocations",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneLocations");

            migrationBuilder.CreateTable(
                name: "WorldLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LocationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldLocations_TypeKey",
                table: "WorldLocations",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorldLocations_TypeKey_Slug",
                table: "WorldLocations",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);
        }
    }
}
