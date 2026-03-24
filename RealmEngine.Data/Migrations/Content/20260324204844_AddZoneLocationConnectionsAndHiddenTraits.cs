using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealmEngine.Data.Migrations.Content
{
    /// <inheritdoc />
    public partial class AddZoneLocationConnectionsAndHiddenTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneLocationConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ToLocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ConnectionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsTraversable = table.Column<bool>(type: "boolean", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneLocationConnections");
        }
    }
}
