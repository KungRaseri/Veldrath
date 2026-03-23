using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Abilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbilityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_Abilities", x => x.Id);
                });

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
                name: "Armors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArmorType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EquipSlot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_Armors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Backgrounds",
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
                    table.PrimaryKey("PK_Backgrounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentRegistry",
                columns: table => new
                {
                    Domain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRegistry", x => new { x.Domain, x.TypeKey, x.Slug });
                });

            migrationBuilder.CreateTable(
                name: "Dialogues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Speaker = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_Dialogues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enchantments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSlot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
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
                    table.PrimaryKey("PK_Enchantments", x => x.Id);
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

            migrationBuilder.CreateTable(
                name: "GameConfigs",
                columns: table => new
                {
                    ConfigKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameConfigs", x => x.ConfigKey);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LootTables",
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
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CostScale = table.Column<float>(type: "real", nullable: false),
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
                    table.PrimaryKey("PK_MaterialProperties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CostScale = table.Column<float>(type: "real", nullable: false),
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
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamePatternSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityPath = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SupportsTraits = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamePatternSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Objectives = table.Column<string>(type: "jsonb", nullable: false),
                    Rewards = table.Column<string>(type: "jsonb", nullable: false),
                    Stats = table.Column<string>(type: "jsonb", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputItemDomain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutputItemSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputQuantity = table.Column<int>(type: "integer", nullable: false),
                    CraftingSkill = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CraftingLevel = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TypeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxRank = table.Column<int>(type: "integer", nullable: false),
                    GoverningAttribute = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Skills", x => x.Id);
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
                name: "Spells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    School = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_Spells", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraitDefinitions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AppliesTo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraitDefinitions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Weapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeaponType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DamageType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HandsRequired = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Weapons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_WorldLocations", x => x.Id);
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
                name: "LootTableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LootTableId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDomain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DropWeight = table.Column<int>(type: "integer", nullable: false),
                    QuantityMin = table.Column<int>(type: "integer", nullable: false),
                    QuantityMax = table.Column<int>(type: "integer", nullable: false),
                    IsGuaranteed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootTableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootTableEntries_LootTables_LootTableId",
                        column: x => x.LootTableId,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NameComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false),
                    BudgetModifier = table.Column<double>(type: "double precision", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NameComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NameComponents_NamePatternSets_SetId",
                        column: x => x.SetId,
                        principalTable: "NamePatternSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NamePatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RarityWeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamePatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NamePatterns_NamePatternSets_SetId",
                        column: x => x.SetId,
                        principalTable: "NamePatternSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDomain = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => new { x.RecipeId, x.ItemDomain, x.ItemSlug });
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_Abilities_TypeKey",
                table: "Abilities",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_TypeKey_Slug",
                table: "Abilities",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

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
                name: "IX_Armors_TypeKey",
                table: "Armors",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Armors_TypeKey_Slug",
                table: "Armors",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_TypeKey",
                table: "Backgrounds",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_TypeKey_Slug",
                table: "Backgrounds",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassAbilityUnlocks_AbilityId",
                table: "ClassAbilityUnlocks",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSpellUnlocks_SpellId",
                table: "ClassSpellUnlocks",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentRegistry_EntityId",
                table: "ContentRegistry",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_TypeKey",
                table: "Dialogues",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_TypeKey_Slug",
                table: "Dialogues",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enchantments_TypeKey",
                table: "Enchantments",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Enchantments_TypeKey_Slug",
                table: "Enchantments",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSets_TypeKey",
                table: "EquipmentSets",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSets_TypeKey_Slug",
                table: "EquipmentSets",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstanceAbilityPools_AbilityId",
                table: "InstanceAbilityPools",
                column: "AbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TypeKey",
                table: "Items",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Items_TypeKey_Slug",
                table: "Items",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LootTableEntries_LootTableId",
                table: "LootTableEntries",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_TypeKey",
                table: "LootTables",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_TypeKey_Slug",
                table: "LootTables",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProperties_TypeKey",
                table: "MaterialProperties",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProperties_TypeKey_Slug",
                table: "MaterialProperties",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_TypeKey",
                table: "Materials",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_TypeKey_Slug",
                table: "Materials",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NameComponents_SetId_ComponentKey_Value",
                table: "NameComponents",
                columns: new[] { "SetId", "ComponentKey", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamePatterns_SetId",
                table: "NamePatterns",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_NamePatternSets_EntityPath",
                table: "NamePatternSets",
                column: "EntityPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TypeKey",
                table: "Organizations",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TypeKey_Slug",
                table: "Organizations",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_TypeKey",
                table: "Quests",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_TypeKey_Slug",
                table: "Quests",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TypeKey",
                table: "Recipes",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_TypeKey_Slug",
                table: "Recipes",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TypeKey",
                table: "Skills",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TypeKey_Slug",
                table: "Skills",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Spells_TypeKey",
                table: "Spells",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_TypeKey_Slug",
                table: "Spells",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_TypeKey",
                table: "Weapons",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_TypeKey_Slug",
                table: "Weapons",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldLocations_TypeKey",
                table: "WorldLocations",
                column: "TypeKey");

            migrationBuilder.CreateIndex(
                name: "IX_WorldLocations_TypeKey_Slug",
                table: "WorldLocations",
                columns: new[] { "TypeKey", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchetypeAbilityPools");

            migrationBuilder.DropTable(
                name: "Armors");

            migrationBuilder.DropTable(
                name: "ClassAbilityUnlocks");

            migrationBuilder.DropTable(
                name: "ClassSpellUnlocks");

            migrationBuilder.DropTable(
                name: "ContentRegistry");

            migrationBuilder.DropTable(
                name: "Dialogues");

            migrationBuilder.DropTable(
                name: "Enchantments");

            migrationBuilder.DropTable(
                name: "EquipmentSets");

            migrationBuilder.DropTable(
                name: "GameConfigs");

            migrationBuilder.DropTable(
                name: "InstanceAbilityPools");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "LootTableEntries");

            migrationBuilder.DropTable(
                name: "MaterialProperties");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "NameComponents");

            migrationBuilder.DropTable(
                name: "NamePatterns");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Quests");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "SpeciesAbilityPools");

            migrationBuilder.DropTable(
                name: "TraitDefinitions");

            migrationBuilder.DropTable(
                name: "Weapons");

            migrationBuilder.DropTable(
                name: "WorldLocations");

            migrationBuilder.DropTable(
                name: "Spells");

            migrationBuilder.DropTable(
                name: "ActorInstances");

            migrationBuilder.DropTable(
                name: "NamePatternSets");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Abilities");

            migrationBuilder.DropTable(
                name: "ActorArchetypes");

            migrationBuilder.DropTable(
                name: "ActorClasses");

            migrationBuilder.DropTable(
                name: "Backgrounds");

            migrationBuilder.DropTable(
                name: "LootTables");

            migrationBuilder.DropTable(
                name: "Species");
        }
    }
}
