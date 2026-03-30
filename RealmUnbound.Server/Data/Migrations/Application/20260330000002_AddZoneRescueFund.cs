using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddZoneRescueFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RescueFundTotal",
                table: "Zones",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RescueFundTotal",
                table: "Zones");
        }
    }
}
