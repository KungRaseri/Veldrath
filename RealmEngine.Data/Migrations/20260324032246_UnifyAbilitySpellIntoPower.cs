using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyAbilitySpellIntoPower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchetypeAbilityPools");

            migrationBuilder.DropTable(
                name: "ClassAbilityUnlocks");

            migrationBuilder.DropTable(
                name: "ClassSpellUnlocks");

            migrationBuilder.DropTable(
                name: "InstanceAbilityPools");

            migrationBuilder.DropTable(
                name: "SpeciesAbilityPools");

            migrationBuilder.DropTable(
                name: "Spells");

            migrationBuilder.DropTable(
                name: "Abilities");

            migrationBuilder.CreateTable(
                name: "Powers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    School = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RequiresItem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Effects = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Powers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchetypePowerPools",
                columns: table => new
                {
                    ArchetypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseChance = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchetypePowerPools", x => new { x.ArchetypeId, x.PowerId });
                    table.ForeignKey(
                        name: "FK_ArchetypePowerPools_ActorArchetypes_ArchetypeId",
                        column: x => x.ArchetypeId,
                        principalTable: "ActorArchetypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchetypePowerPools_Powers_PowerId",
                        column: x => x.PowerId,
                        principalTable: "Powers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassPowerUnlocks",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelRequired = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPowerUnlocks", x => new { x.ClassId, x.PowerId });
                    table.ForeignKey(
                        name: "FK_ClassPowerUnlocks_ActorClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "ActorClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassPowerUnlocks_Powers_PowerId",
                        column: x => x.PowerId,
                        principalTable: "Powers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstancePowerPools",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstancePowerPools", x => new { x.InstanceId, x.PowerId });
                    table.ForeignKey(
                        name: "FK_InstancePowerPools_ActorInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "ActorInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstancePowerPools_Powers_PowerId",
                        column: x => x.PowerId,
                        principalTable: "Powers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpeciesPowerPools",
                columns: table => new
                {
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: false),
                    PowerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeciesPowerPools", x => new { x.SpeciesId, x.PowerId });
                    table.ForeignKey(
                        name: "FK_SpeciesPowerPools_Powers_PowerId",
                        column: x => x.PowerId,
                        principalTable: "Powers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpeciesPowerPools_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchetypePowerPools_PowerId",
                table: "ArchetypePowerPools",
                column: "PowerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPowerUnlocks_PowerId",
                table: "ClassPowerUnlocks",
                column: "PowerId");

            migrationBuilder.CreateIndex(
                name: "IX_InstancePowerPools_PowerId",
                table: "InstancePowerPools",
                column: "PowerId");

            migrationBuilder.CreateIndex(
                name: "IX_Powers_TypeKey",
                table: "Powers",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Powers_TypeKey_Slug",
                table: "Powers",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesPowerPools_PowerId",
                table: "SpeciesPowerPools",
                column: "PowerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchetypePowerPools");

            migrationBuilder.DropTable(
                name: "ClassPowerUnlocks");

            migrationBuilder.DropTable(
                name: "InstancePowerPools");

            migrationBuilder.DropTable(
                name: "SpeciesPowerPools");

            migrationBuilder.DropTable(
                name: "Powers");

            migrationBuilder.CreateTable(
                name: "Abilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Effects = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    School = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchetypeAbilityPools",
                columns: table => new
                {
                    ArchetypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseChance = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchetypeAbilityPools", x => new { x.ArchetypeId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_ArchetypeAbilityPools_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchetypeAbilityPools_ActorArchetypes_ArchetypeId",
                        column: x => x.ArchetypeId,
                        principalTable: "ActorArchetypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassAbilityUnlocks",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelRequired = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassAbilityUnlocks", x => new { x.ClassId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_ClassAbilityUnlocks_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassAbilityUnlocks_ActorClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "ActorClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstanceAbilityPools",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceAbilityPools", x => new { x.InstanceId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_InstanceAbilityPools_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstanceAbilityPools_ActorInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalTable: "ActorInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpeciesAbilityPools",
                columns: table => new
                {
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeciesAbilityPools", x => new { x.SpeciesId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_SpeciesAbilityPools_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpeciesAbilityPools_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_TypeKey",
                table: "Abilities",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_TypeKey_Slug",
                table: "Abilities",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchetypeAbilityPools_AbilityId",
                table: "ArchetypeAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassAbilityUnlocks_AbilityId",
                table: "ClassAbilityUnlocks",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSpellUnlocks_SpellId",
                table: "ClassSpellUnlocks",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_InstanceAbilityPools_AbilityId",
                table: "InstanceAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesAbilityPools_AbilityId",
                table: "SpeciesAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_TypeKey",
                table: "Spells",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_TypeKey_Slug",
                table: "Spells",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);
        }
    }
}
