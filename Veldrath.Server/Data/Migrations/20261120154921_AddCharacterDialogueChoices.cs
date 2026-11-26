using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veldrath.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterDialogueChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterDialogueChoices",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OptionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChosenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterDialogueChoices", x => new { x.CharacterId, x.SpeakerSlug, x.OptionKey });
                    table.ForeignKey(
                        name: "FK_CharacterDialogueChoices_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterDialogueChoices");
        }
    }
}
