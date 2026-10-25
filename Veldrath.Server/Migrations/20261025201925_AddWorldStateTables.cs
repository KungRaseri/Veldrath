using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnemyInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneGroup = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArchetypeSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    CurrentHealth = table.Column<int>(type: "integer", nullable: false),
                    MaxHealth = table.Column<int>(type: "integer", nullable: false),
                    BaseXp = table.Column<int>(type: "integer", nullable: false),
                    GoldReward = table.Column<int>(type: "integer", nullable: false),
                    IsAlive = table.Column<bool>(type: "boolean", nullable: false),
                    SpawnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnemyInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CombatSessions",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnemyInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneGroup = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    PlayerDefending = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatSessions", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_CombatSessions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatSessions_EnemyInstances_EnemyInstanceId",
                        column: x => x.EnemyInstanceId,
                        principalTable: "EnemyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnemyParticipants",
                columns: table => new
                {
                    EnemyInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Damage = table.Column<int>(type: "integer", nullable: false),
                    LastAttackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnemyParticipants", x => new { x.EnemyInstanceId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_EnemyParticipants_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnemyParticipants_EnemyInstances_EnemyInstanceId",
                        column: x => x.EnemyInstanceId,
                        principalTable: "EnemyInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatSessions_DisconnectedAt",
                table: "CombatSessions",
                column: "DisconnectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CombatSessions_EnemyInstanceId",
                table: "CombatSessions",
                column: "EnemyInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_EnemyInstances_IsAlive_RespawnAt",
                table: "EnemyInstances",
                columns: new[] { "IsAlive", "RespawnAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnemyInstances_ZoneGroup_LocationSlug_ArchetypeSlug_Spawned~",
                table: "EnemyInstances",
                columns: new[] { "ZoneGroup", "LocationSlug", "ArchetypeSlug", "SpawnedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnemyParticipants_CharacterId",
                table: "EnemyParticipants",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombatSessions");

            migrationBuilder.DropTable(
                name: "EnemyParticipants");

            migrationBuilder.DropTable(
                name: "EnemyInstances");
        }
    }
}
