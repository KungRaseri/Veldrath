using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Persistence;

/// <summary>
/// Standalone EF Core context for the game content catalog (abilities, enemies, items, etc.).
/// Does not include ASP.NET Core Identity tables — use this in tools like RealmForge or
/// any consumer that needs to read/write game content without the server auth stack.
/// Always backed by PostgreSQL.
/// </summary>
public class ContentDbContext : DbContext
{
    /// <summary>Initialises a new <see cref="ContentDbContext"/> with the given options.</summary>
    public ContentDbContext(DbContextOptions<ContentDbContext> options) : base(options) { }

    // ── Support tables ────────────────────────────────────────────────────────
    /// <summary>Cross-domain registry mapping slugs to their owning entity rows.</summary>
    public DbSet<ContentRegistry> ContentRegistry => Set<ContentRegistry>();
    /// <summary>Vocabulary of trait keys and their expected value types.</summary>
    public DbSet<TraitDefinition> TraitDefinitions => Set<TraitDefinition>();

    // ── Catalog content ───────────────────────────────────────────────────────
    /// <summary>Active and passive character abilities.</summary>
    public DbSet<Ability> Abilities => Set<Ability>();
    /// <summary>Biological species definitions.</summary>
    public DbSet<Species> Species => Set<Species>();
    /// <summary>Actor class definitions (replaces CharacterClasses).</summary>
    public DbSet<ActorClass> ActorClasses => Set<ActorClass>();
    /// <summary>Composed actor templates (replaces separate Enemies and Npcs).</summary>
    public DbSet<ActorArchetype> ActorArchetypes => Set<ActorArchetype>();
    /// <summary>Named unique actor instances that override an archetype.</summary>
    public DbSet<ActorInstance> ActorInstances => Set<ActorInstance>();
    /// <summary>Weapons.</summary>
    public DbSet<Weapon> Weapons => Set<Weapon>();
    /// <summary>Armor pieces.</summary>
    public DbSet<Armor> Armors => Set<Armor>();
    /// <summary>General items (consumables, quest items, misc).</summary>
    public DbSet<Item> Items => Set<Item>();
    /// <summary>Crafting materials.</summary>
    public DbSet<Material> Materials => Set<Material>();
    /// <summary>Equipment enchantments.</summary>
    public DbSet<Enchantment> Enchantments => Set<Enchantment>();
    /// <summary>Character skills.</summary>
    public DbSet<Skill> Skills => Set<Skill>();
    /// <summary>Magic spells.</summary>
    public DbSet<Spell> Spells => Set<Spell>();
    /// <summary>Character background origins.</summary>
    public DbSet<Background> Backgrounds => Set<Background>();
    /// <summary>Quests.</summary>
    public DbSet<Quest> Quests => Set<Quest>();
    /// <summary>Crafting recipes.</summary>
    public DbSet<Recipe> Recipes => Set<Recipe>();
    /// <summary>Loot tables.</summary>
    public DbSet<LootTable> LootTables => Set<LootTable>();
    /// <summary>Factions and guilds.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();
    /// <summary>Material property definitions.</summary>
    public DbSet<MaterialProperty> MaterialProperties => Set<MaterialProperty>();
    /// <summary>World locations and regions.</summary>
    public DbSet<WorldLocation> WorldLocations => Set<WorldLocation>();
    /// <summary>NPC dialogue trees.</summary>
    public DbSet<Dialogue> Dialogues => Set<Dialogue>();

    // ── Junction tables ───────────────────────────────────────────────────────
    /// <summary>Many-to-many: species ↔ innate abilities.</summary>
    public DbSet<SpeciesAbilityPool> SpeciesAbilityPools => Set<SpeciesAbilityPool>();
    /// <summary>Many-to-many: actor archetypes ↔ abilities.</summary>
    public DbSet<ArchetypeAbilityPool> ArchetypeAbilityPools => Set<ArchetypeAbilityPool>();
    /// <summary>Many-to-many: actor instances ↔ override abilities.</summary>
    public DbSet<InstanceAbilityPool> InstanceAbilityPools => Set<InstanceAbilityPool>();
    /// <summary>Many-to-many: actor classes ↔ ability unlocks.</summary>
    public DbSet<ClassAbilityUnlock> ClassAbilityUnlocks => Set<ClassAbilityUnlock>();
    /// <summary>Many-to-many: actor classes ↔ spell unlocks.</summary>
    public DbSet<ClassSpellUnlock> ClassSpellUnlocks => Set<ClassSpellUnlock>();
    /// <summary>Equipment set definitions.</summary>
    public DbSet<EquipmentSetEntry> EquipmentSets => Set<EquipmentSetEntry>();
    /// <summary>Loot table line items.</summary>
    public DbSet<LootTableEntry> LootTableEntries => Set<LootTableEntry>();
    /// <summary>Recipe ingredient lines.</summary>
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // ── Name patterns ─────────────────────────────────────────────────────────
    /// <summary>Named sets of procedural name-generation patterns.</summary>
    public DbSet<NamePatternSet> NamePatternSets => Set<NamePatternSet>();
    /// <summary>Individual pattern templates within a set.</summary>
    public DbSet<NamePattern> NamePatterns => Set<NamePattern>();
    /// <summary>Individual name component values (prefixes, suffixes, etc.).</summary>
    public DbSet<NameComponent> NameComponents => Set<NameComponent>();

    // ── System configuration ──────────────────────────────────────────────────
    /// <summary>Key-value JSON configuration blobs for game tuning.</summary>
    public DbSet<GameConfig> GameConfigs => Set<GameConfig>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ContentModelConfiguration.Configure(builder);
    }
}
