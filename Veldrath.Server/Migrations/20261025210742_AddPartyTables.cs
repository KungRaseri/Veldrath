using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaderCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LootRule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "contribution")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parties_Characters_LeaderCharacterId",
                        column: x => x.LeaderCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Characters_InvitedByCharacterId",
                        column: x => x.InvitedByCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Characters_InvitedCharacterId",
                        column: x => x.InvitedCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyInvites_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartyMembers",
                columns: table => new
                {
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMembers", x => new { x.PartyId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_PartyMembers_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_LeaderCharacterId",
                table: "Parties",
                column: "LeaderCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_ExpiresAt",
                table: "PartyInvites",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_InvitedByCharacterId",
                table: "PartyInvites",
                column: "InvitedByCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_InvitedCharacterId",
                table: "PartyInvites",
                column: "InvitedCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyInvites_PartyId_InvitedCharacterId",
                table: "PartyInvites",
                columns: new[] { "PartyId", "InvitedCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_CharacterId",
                table: "PartyMembers",
                column: "CharacterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyInvites");

            migrationBuilder.DropTable(
                name: "PartyMembers");

            migrationBuilder.DropTable(
                name: "Parties");
        }
    }
}
