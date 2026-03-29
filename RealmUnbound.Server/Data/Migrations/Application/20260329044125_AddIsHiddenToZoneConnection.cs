using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddIsHiddenToZoneConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "ZoneConnections",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "ZoneConnections");
        }
    }
}
