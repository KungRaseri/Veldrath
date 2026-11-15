using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneInstanceId",
                table: "PlayerSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ZoneInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneGroup = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastOccupiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneInstances_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneInstanceParticipants",
                columns: table => new
                {
                    ZoneInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneInstanceParticipants", x => new { x.ZoneInstanceId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_ZoneInstanceParticipants_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZoneInstanceParticipants_ZoneInstances_ZoneInstanceId",
                        column: x => x.ZoneInstanceId,
                        principalTable: "ZoneInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_ZoneInstanceId",
                table: "PlayerSessions",
                column: "ZoneInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneInstanceParticipants_CharacterId",
                table: "ZoneInstanceParticipants",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneInstances_State_LastOccupiedAt",
                table: "ZoneInstances",
                columns: new[] { "State", "LastOccupiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneInstances_ZoneId_CreatedByCharacterId",
                table: "ZoneInstances",
                columns: new[] { "ZoneId", "CreatedByCharacterId" },
                unique: true,
                filter: "\"OwnerPartyId\" IS NULL AND \"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneInstances_ZoneId_OwnerPartyId",
                table: "ZoneInstances",
                columns: new[] { "ZoneId", "OwnerPartyId" },
                unique: true,
                filter: "\"OwnerPartyId\" IS NOT NULL AND \"State\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneInstanceParticipants");

            migrationBuilder.DropTable(
                name: "ZoneInstances");

            migrationBuilder.DropIndex(
                name: "IX_PlayerSessions_ZoneInstanceId",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "ZoneInstanceId",
                table: "PlayerSessions");
        }
    }
}
