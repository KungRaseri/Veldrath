using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealmEngine.Data.Migrations.ContentDb
{
    /// <inheritdoc />
    public partial class RemoveZoneLocationConnectionsAndLocationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneLocationConnections");

            migrationBuilder.DropColumn(
                name: "LocationType",
                table: "ZoneLocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationType",
                table: "ZoneLocations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ZoneLocationConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConnectionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    IsTraversable = table.Column<bool>(type: "boolean", nullable: false),
                    ToLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneLocationConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneLocationConnections_FromLocationSlug",
                table: "ZoneLocationConnections",
                column: "FromLocationSlug");
        }
    }
}
