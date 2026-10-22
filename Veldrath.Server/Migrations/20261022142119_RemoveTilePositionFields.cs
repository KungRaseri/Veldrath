using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTilePositionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMovedAt",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "TileX",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "TileY",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "TileX",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "TileY",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "TileZoneId",
                table: "Characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMovedAt",
                table: "PlayerSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "TileX",
                table: "PlayerSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TileY",
                table: "PlayerSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TileX",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TileY",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TileZoneId",
                table: "Characters",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
