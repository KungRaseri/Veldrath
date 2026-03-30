using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddCharacterDifficultyMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DifficultyMode",
                table: "Characters",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DifficultyMode",
                table: "Characters");
        }
    }
}
