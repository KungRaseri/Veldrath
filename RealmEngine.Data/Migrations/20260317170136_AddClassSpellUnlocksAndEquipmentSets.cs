using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSpellUnlocksAndEquipmentSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassSpellUnlocks",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpellId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelRequired = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSpellUnlocks", x => new { x.ClassId, x.SpellId });
                    table.ForeignKey(
                        name: "FK_ClassSpellUnlocks_ActorClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "ActorClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSpellUnlocks_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSpellUnlocks_SpellId",
                table: "ClassSpellUnlocks",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSets_TypeKey",
                table: "EquipmentSets",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSets_TypeKey_Slug",
                table: "EquipmentSets",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassSpellUnlocks");

            migrationBuilder.DropTable(
                name: "EquipmentSets");
        }
    }
}
