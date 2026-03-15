using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmUnbound.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassAbilityUnlocks_CharacterClasses_ClassId",
                table: "ClassAbilityUnlocks");

            migrationBuilder.DropTable(
                name: "CharacterClasses");

            migrationBuilder.DropTable(
                name: "EnemyAbilityPools");

            migrationBuilder.DropTable(
                name: "NpcAbilities");

            migrationBuilder.DropTable(
                name: "Enemies");

            migrationBuilder.DropTable(
                name: "Npcs");

            migrationBuilder.CreateTable(
                name: "ActorClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HitDie = table.Column<int>(type: "integer", nullable: false),
                    PrimaryStat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActorClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Species",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Species", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActorArchetypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeciesId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackgroundId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    LootTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActorArchetypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActorArchetypes_ActorClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "ActorClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActorArchetypes_Backgrounds_BackgroundId",
                        column: x => x.BackgroundId,
                        principalTable: "Backgrounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActorArchetypes_LootTables_LootTableId",
                        column: x => x.LootTableId,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActorArchetypes_Species_SpeciesId",
                        column: x => x.SpeciesId,
                        principalTable: "Species",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "ActorInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchetypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelOverride = table.Column<int>(type: "integer", nullable: true),
                    LootTableOverride = table.Column<Guid>(type: "uuid", nullable: true),
                    FactionOverride = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatOverrides = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActorInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActorInstances_ActorArchetypes_ArchetypeId",
                        column: x => x.ArchetypeId,
                        principalTable: "ActorArchetypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActorInstances_LootTables_LootTableOverride",
                        column: x => x.LootTableOverride,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_BackgroundId",
                table: "ActorArchetypes",
                column: "BackgroundId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_ClassId",
                table: "ActorArchetypes",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_LootTableId",
                table: "ActorArchetypes",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_SpeciesId",
                table: "ActorArchetypes",
                column: "SpeciesId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_TypeKey",
                table: "ActorArchetypes",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_ActorArchetypes_TypeKey_Slug",
                table: "ActorArchetypes",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActorClasses_TypeKey",
                table: "ActorClasses",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_ActorClasses_TypeKey_Slug",
                table: "ActorClasses",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActorInstances_ArchetypeId",
                table: "ActorInstances",
                column: "ArchetypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActorInstances_LootTableOverride",
                table: "ActorInstances",
                column: "LootTableOverride");

            migrationBuilder.CreateIndex(
                name: "IX_ActorInstances_TypeKey",
                table: "ActorInstances",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_ActorInstances_TypeKey_Slug",
                table: "ActorInstances",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchetypeAbilityPools_AbilityId",
                table: "ArchetypeAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_InstanceAbilityPools_AbilityId",
                table: "InstanceAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Species_TypeKey",
                table: "Species",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Species_TypeKey_Slug",
                table: "Species",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeciesAbilityPools_AbilityId",
                table: "SpeciesAbilityPools",
                column: "AbilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassAbilityUnlocks_ActorClasses_ClassId",
                table: "ClassAbilityUnlocks",
                column: "ClassId",
                principalTable: "ActorClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassAbilityUnlocks_ActorClasses_ClassId",
                table: "ClassAbilityUnlocks");

            migrationBuilder.DropTable(
                name: "ArchetypeAbilityPools");

            migrationBuilder.DropTable(
                name: "InstanceAbilityPools");

            migrationBuilder.DropTable(
                name: "SpeciesAbilityPools");

            migrationBuilder.DropTable(
                name: "ActorInstances");

            migrationBuilder.DropTable(
                name: "ActorArchetypes");

            migrationBuilder.DropTable(
                name: "ActorClasses");

            migrationBuilder.DropTable(
                name: "Species");

            migrationBuilder.CreateTable(
                name: "CharacterClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HitDie = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PrimaryStat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enemies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LootTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Properties = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enemies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enemies_LootTables_LootTableId",
                        column: x => x.LootTableId,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Faction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Schedule = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnemyAbilityPools",
                columns: table => new
                {
                    EnemyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseChance = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnemyAbilityPools", x => new { x.EnemyId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_EnemyAbilityPools_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnemyAbilityPools_Enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalTable: "Enemies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAbilities",
                columns: table => new
                {
                    NpcId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAbilities", x => new { x.NpcId, x.AbilityId });
                    table.ForeignKey(
                        name: "FK_NpcAbilities_Abilities_AbilityId",
                        column: x => x.AbilityId,
                        principalTable: "Abilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcAbilities_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_TypeKey",
                table: "CharacterClasses",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_TypeKey_Slug",
                table: "CharacterClasses",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enemies_LootTableId",
                table: "Enemies",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Enemies_TypeKey",
                table: "Enemies",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Enemies_TypeKey_Slug",
                table: "Enemies",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnemyAbilityPools_AbilityId",
                table: "EnemyAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAbilities_AbilityId",
                table: "NpcAbilities",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_TypeKey",
                table: "Npcs",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_TypeKey_Slug",
                table: "Npcs",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassAbilityUnlocks_CharacterClasses_ClassId",
                table: "ClassAbilityUnlocks",
                column: "ClassId",
                principalTable: "CharacterClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
