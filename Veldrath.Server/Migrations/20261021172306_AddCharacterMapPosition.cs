using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterMapPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentMapId",
                table: "PlayerSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeId",
                table: "PlayerSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentMapId",
                table: "Characters",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeId",
                table: "Characters",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentMapId",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "PlayerSessions");

            migrationBuilder.DropColumn(
                name: "CurrentMapId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "Characters");
        }
    }
}
