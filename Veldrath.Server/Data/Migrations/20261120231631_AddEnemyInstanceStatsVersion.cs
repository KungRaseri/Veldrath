using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnemyInstanceStatsVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatsVersion",
                table: "EnemyInstances",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatsVersion",
                table: "EnemyInstances");
        }
    }
}
