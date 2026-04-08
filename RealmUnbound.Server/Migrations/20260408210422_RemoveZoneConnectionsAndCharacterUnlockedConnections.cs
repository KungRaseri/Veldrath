using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealmUnbound.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveZoneConnectionsAndCharacterUnlockedConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterUnlockedConnections");

            migrationBuilder.DropTable(
                name: "ZoneConnections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterUnlockedConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<int>(type: "integer", nullable: false),
                    UnlockSource = table.Column<string>(type: "text", nullable: false),
                    UnlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterUnlockedConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterUnlockedConnections_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneConnections",
                columns: table => new
                {
                    FromZoneId = table.Column<string>(type: "character varying(64)", nullable: false),
                    ToZoneId = table.Column<string>(type: "character varying(64)", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneConnections", x => new { x.FromZoneId, x.ToZoneId });
                    table.ForeignKey(
                        name: "FK_ZoneConnections_Zones_FromZoneId",
                        column: x => x.FromZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZoneConnections_Zones_ToZoneId",
                        column: x => x.ToZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterUnlockedConnections_CharacterId",
                table: "CharacterUnlockedConnections",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneConnections_ToZoneId",
                table: "ZoneConnections",
                column: "ToZoneId");
        }
    }
}
