using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneInstanceLocationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneInstanceLocationStates",
                columns: table => new
                {
                    ZoneInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsCleared = table.Column<bool>(type: "boolean", nullable: false),
                    ClearedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstClearedByCharacterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneInstanceLocationStates", x => new { x.ZoneInstanceId, x.LocationSlug });
                    table.ForeignKey(
                        name: "FK_ZoneInstanceLocationStates_ZoneInstances_ZoneInstanceId",
                        column: x => x.ZoneInstanceId,
                        principalTable: "ZoneInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneInstanceLocationStates_ZoneInstanceId_IsCleared",
                table: "ZoneInstanceLocationStates",
                columns: new[] { "ZoneInstanceId", "IsCleared" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneInstanceLocationStates");
        }
    }
}
