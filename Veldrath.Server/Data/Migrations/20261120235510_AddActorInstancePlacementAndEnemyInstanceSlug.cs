using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActorInstancePlacementAndEnemyInstanceSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstanceSlug",
                table: "EnemyInstances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstanceSlug",
                table: "EnemyInstances");
        }
    }
}
