namespace RealmForge.Services;

/// <summary>
/// Authoritative static manifest of all content domains, type keys, and their backing DB table names.
/// This drives the RealmForge sidebar tree structure completely independently of what exists in
/// ContentRegistry — every category is always visible whether or not any entities have been created yet.
/// </summary>
public static class ContentDomainCatalog
{
    /// <summary>
    /// A single type-key slot in the content hierarchy.
    /// </summary>
    /// <param name="DomainGroup">Top-level tree node key — groups related domains (e.g. "items/weapons").</param>
    /// <param name="DomainLabel">Human-readable top-level label shown in the tree (e.g. "Items › Weapons").</param>
    /// <param name="Domain">Domain path stored in ContentRegistry (e.g. "items/weapons").</param>
    /// <param name="TypeKey">TypeKey stored in ContentRegistry and on the entity (e.g. "swords").</param>
    /// <param name="TableName">EF Core DbSet table name (e.g. "Weapons").</param>
    /// <param name="TypeKeyLabel">Human-readable category label (e.g. "Swords").</param>
    public sealed record DomainEntry(
        string DomainGroup,
        string DomainLabel,
        string Domain,
        string TypeKey,
        string TableName,
        string TypeKeyLabel);

    public static IReadOnlyList<DomainEntry> All { get; } =
    [
        // ── Abilities ────────────────────────────────────────────────────────
        new("abilities", "Abilities", "abilities", "active/offensive",  "Abilities", "Active — Offensive"),
        new("abilities", "Abilities", "abilities", "active/defensive",  "Abilities", "Active — Defensive"),
        new("abilities", "Abilities", "abilities", "active/support",    "Abilities", "Active — Support"),
        new("abilities", "Abilities", "abilities", "passive/offensive", "Abilities", "Passive — Offensive"),
        new("abilities", "Abilities", "abilities", "passive/defensive", "Abilities", "Passive — Defensive"),
        new("abilities", "Abilities", "abilities", "passive/utility",   "Abilities", "Passive — Utility"),
        new("abilities", "Abilities", "abilities", "reactive",          "Abilities", "Reactive"),
        new("abilities", "Abilities", "abilities", "ultimate",          "Abilities", "Ultimate"),

        // ── Spells ───────────────────────────────────────────────────────────
        new("spells", "Spells", "spells", "offensive", "Spells", "Offensive"),
        new("spells", "Spells", "spells", "defensive", "Spells", "Defensive"),
        new("spells", "Spells", "spells", "utility",   "Spells", "Utility"),
        new("spells", "Spells", "spells", "healing",   "Spells", "Healing"),
        new("spells", "Spells", "spells", "summoning", "Spells", "Summoning"),

        // ── Actor › Species ────────────────────────────────────────────────
        new("actors/species", "Actors › Species", "actors/species", "humanoid",  "Species", "Humanoid"),
        new("actors/species", "Actors › Species", "actors/species", "beast",     "Species", "Beast"),
        new("actors/species", "Actors › Species", "actors/species", "undead",    "Species", "Undead"),
        new("actors/species", "Actors › Species", "actors/species", "demon",     "Species", "Demon"),
        new("actors/species", "Actors › Species", "actors/species", "dragon",    "Species", "Dragon"),
        new("actors/species", "Actors › Species", "actors/species", "elemental", "Species", "Elemental"),
        new("actors/species", "Actors › Species", "actors/species", "construct", "Species", "Construct"),

        // ── Actor › Classes ────────────────────────────────────────────────
        new("actors/classes", "Actors › Classes", "actors/classes", "warriors", "ActorClasses", "Warriors"),
        new("actors/classes", "Actors › Classes", "actors/classes", "rogues",   "ActorClasses", "Rogues"),
        new("actors/classes", "Actors › Classes", "actors/classes", "casters",  "ActorClasses", "Casters"),
        new("actors/classes", "Actors › Classes", "actors/classes", "hybrids",  "ActorClasses", "Hybrids"),

        // ── Actor › Backgrounds ────────────────────────────────────────────
        new("actors/backgrounds", "Actors › Backgrounds", "actors/backgrounds", "common",    "Backgrounds", "Common"),
        new("actors/backgrounds", "Actors › Backgrounds", "actors/backgrounds", "noble",     "Backgrounds", "Noble"),
        new("actors/backgrounds", "Actors › Backgrounds", "actors/backgrounds", "outlander", "Backgrounds", "Outlander"),
        new("actors/backgrounds", "Actors › Backgrounds", "actors/backgrounds", "criminal",  "Backgrounds", "Criminal"),
        new("actors/backgrounds", "Actors › Backgrounds", "actors/backgrounds", "scholar",   "Backgrounds", "Scholar"),

        // ── Actor › Skills ─────────────────────────────────────────────────
        new("actors/skills", "Actors › Skills", "actors/skills", "combat",      "Skills", "Combat"),
        new("actors/skills", "Actors › Skills", "actors/skills", "crafting",    "Skills", "Crafting"),
        new("actors/skills", "Actors › Skills", "actors/skills", "social",      "Skills", "Social"),
        new("actors/skills", "Actors › Skills", "actors/skills", "exploration", "Skills", "Exploration"),

        // ── Actor › Archetypes ─────────────────────────────────────────────
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "humanoids/bandits",   "ActorArchetypes", "Humanoids — Bandits"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "humanoids/soldiers",  "ActorArchetypes", "Humanoids — Soldiers"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "beasts/wolves",       "ActorArchetypes", "Beasts — Wolves"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "beasts/bears",        "ActorArchetypes", "Beasts — Bears"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "undead/skeletons",    "ActorArchetypes", "Undead — Skeletons"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "undead/zombies",      "ActorArchetypes", "Undead — Zombies"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "demons",              "ActorArchetypes", "Demons"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "elementals",          "ActorArchetypes", "Elementals"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "constructs",          "ActorArchetypes", "Constructs"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "merchants",           "ActorArchetypes", "Merchants"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "questgivers",         "ActorArchetypes", "Quest Givers"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "guards",              "ActorArchetypes", "Guards"),
        new("actors/archetypes", "Actors › Archetypes", "actors/archetypes", "wanderers",           "ActorArchetypes", "Wanderers"),

        // ── Actor › Instances ──────────────────────────────────────────────
        new("actors/instances", "Actors › Instances", "actors/instances", "boss",   "ActorInstances", "Bosses"),
        new("actors/instances", "Actors › Instances", "actors/instances", "story",  "ActorInstances", "Story Characters"),
        new("actors/instances", "Actors › Instances", "actors/instances", "unique", "ActorInstances", "Unique"),

        // ── Items › Weapons ───────────────────────────────────────────────────
        new("items/weapons", "Items › Weapons", "items/weapons", "swords",    "Weapons", "Swords"),
        new("items/weapons", "Items › Weapons", "items/weapons", "axes",      "Weapons", "Axes"),
        new("items/weapons", "Items › Weapons", "items/weapons", "bows",      "Weapons", "Bows"),
        new("items/weapons", "Items › Weapons", "items/weapons", "staves",    "Weapons", "Staves"),
        new("items/weapons", "Items › Weapons", "items/weapons", "daggers",   "Weapons", "Daggers"),
        new("items/weapons", "Items › Weapons", "items/weapons", "maces",     "Weapons", "Maces"),
        new("items/weapons", "Items › Weapons", "items/weapons", "spears",    "Weapons", "Spears"),
        new("items/weapons", "Items › Weapons", "items/weapons", "crossbows", "Weapons", "Crossbows"),

        // ── Items › Armor ─────────────────────────────────────────────────────
        new("items/armor", "Items › Armor", "items/armor", "light",   "Armors", "Light"),
        new("items/armor", "Items › Armor", "items/armor", "medium",  "Armors", "Medium"),
        new("items/armor", "Items › Armor", "items/armor", "heavy",   "Armors", "Heavy"),
        new("items/armor", "Items › Armor", "items/armor", "shields", "Armors", "Shields"),

        // ── Items › Materials ─────────────────────────────────────────────────
        new("items/materials", "Items › Materials", "items/materials", "metals",   "Materials", "Metals"),
        new("items/materials", "Items › Materials", "items/materials", "woods",    "Materials", "Woods"),
        new("items/materials", "Items › Materials", "items/materials", "fabrics",  "Materials", "Fabrics"),
        new("items/materials", "Items › Materials", "items/materials", "gems",     "Materials", "Gems"),
        new("items/materials", "Items › Materials", "items/materials", "leathers", "Materials", "Leathers"),

        // ── Items › Material Properties ───────────────────────────────────────
        new("items/material-properties", "Items › Material Properties", "items/material-properties", "metals",   "MaterialProperties", "Metals"),
        new("items/material-properties", "Items › Material Properties", "items/material-properties", "woods",    "MaterialProperties", "Woods"),
        new("items/material-properties", "Items › Material Properties", "items/material-properties", "fabrics",  "MaterialProperties", "Fabrics"),
        new("items/material-properties", "Items › Material Properties", "items/material-properties", "gems",     "MaterialProperties", "Gems"),
        new("items/material-properties", "Items › Material Properties", "items/material-properties", "leathers", "MaterialProperties", "Leathers"),

        // ── Items › Enchantments ──────────────────────────────────────────────
        new("items/enchantments", "Items › Enchantments", "items/enchantments", "weapon",  "Enchantments", "Weapon"),
        new("items/enchantments", "Items › Enchantments", "items/enchantments", "armor",   "Enchantments", "Armor"),
        new("items/enchantments", "Items › Enchantments", "items/enchantments", "utility", "Enchantments", "Utility"),
        new("items/enchantments", "Items › Enchantments", "items/enchantments", "cursed",  "Enchantments", "Cursed"),

        // ── Items › Consumables ───────────────────────────────────────────────
        new("items/consumables", "Items › Consumables", "items/consumables", "potions", "Items", "Potions"),
        new("items/consumables", "Items › Consumables", "items/consumables", "food",    "Items", "Food"),
        new("items/consumables", "Items › Consumables", "items/consumables", "scrolls", "Items", "Scrolls"),

        // ── World › Locations ─────────────────────────────────────────────────
        new("world/locations", "World › Locations", "world/locations", "towns",              "WorldLocations", "Towns"),
        new("world/locations", "World › Locations", "world/locations", "dungeons",           "WorldLocations", "Dungeons"),
        new("world/locations", "World › Locations", "world/locations", "wilderness",         "WorldLocations", "Wilderness"),
        new("world/locations", "World › Locations", "world/locations", "points-of-interest", "WorldLocations", "Points of Interest"),

        // ── World › Organizations ─────────────────────────────────────────────
        new("world/organizations", "World › Organizations", "world/organizations", "factions",         "Organizations", "Factions"),
        new("world/organizations", "World › Organizations", "world/organizations", "guilds",           "Organizations", "Guilds"),
        new("world/organizations", "World › Organizations", "world/organizations", "cults",            "Organizations", "Cults"),
        new("world/organizations", "World › Organizations", "world/organizations", "merchant-leagues", "Organizations", "Merchant Leagues"),

        // ── World › Quests ────────────────────────────────────────────────────
        new("world/quests", "World › Quests", "world/quests", "main-story",  "Quests", "Main Story"),
        new("world/quests", "World › Quests", "world/quests", "side-quests", "Quests", "Side Quests"),
        new("world/quests", "World › Quests", "world/quests", "daily",       "Quests", "Daily"),
        new("world/quests", "World › Quests", "world/quests", "guild",       "Quests", "Guild"),

        // ── World › Dialogues ─────────────────────────────────────────────────
        new("world/dialogues", "World › Dialogues", "world/dialogues", "merchants",   "Dialogues", "Merchants"),
        new("world/dialogues", "World › Dialogues", "world/dialogues", "questgivers", "Dialogues", "Quest Givers"),
        new("world/dialogues", "World › Dialogues", "world/dialogues", "story",       "Dialogues", "Story"),
        new("world/dialogues", "World › Dialogues", "world/dialogues", "ambient",     "Dialogues", "Ambient"),

        // ── Loot Tables ───────────────────────────────────────────────────────
        new("loot-tables", "Loot Tables", "loot-tables", "actors",   "LootTables", "Actors"),
        new("loot-tables", "Loot Tables", "loot-tables", "treasure", "LootTables", "Treasure"),
        new("loot-tables", "Loot Tables", "loot-tables", "vendors",  "LootTables", "Vendors"),
        new("loot-tables", "Loot Tables", "loot-tables", "events",   "LootTables", "Events"),

        // ── Recipes ───────────────────────────────────────────────────────────
        new("recipes", "Recipes", "recipes", "crafting", "Recipes", "Crafting"),
        new("recipes", "Recipes", "recipes", "alchemy",  "Recipes", "Alchemy"),
        new("recipes", "Recipes", "recipes", "cooking",  "Recipes", "Cooking"),
    ];
}
