using System.Text.Json;

namespace RealmUnbound.Contracts.Content;

// ── Content Browse ─────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight summary row used in paged content browse lists.
/// Returned by <c>GET /api/content/browse?type=…</c>.
/// </summary>
public record ContentSummaryDto(
    Guid Id,
    string Slug,
    string? DisplayName,
    string TypeKey,
    string ContentType,
    int RarityWeight,
    bool IsActive,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Full content entity detail returned by <c>GET /api/content/browse/{type}/{slug}</c>.
/// <see cref="Payload"/> is the complete entity serialized as camelCase JSON so that
/// field names align with <see cref="ContentSchema.ContentFieldDescriptor.Name"/> paths.
/// </summary>
public record ContentDetailDto(ContentSummaryDto Summary, JsonElement Payload);

/// <summary>Top-level info about one content type — used by the schema listing endpoint.</summary>
public record ContentTypeInfoDto(string ContentType, string DisplayLabel, string Description);

// ── Ability ───────────────────────────────────────────────────────────────────

/// <summary>A single active or passive ability available to player characters.</summary>
public record AbilityDto(
    string Slug,
    string DisplayName,
    string AbilityType,
    string Description,
    int ManaCost,
    int Cooldown,
    int Range,
    int RarityWeight,
    bool IsPassive,
    int RequiredLevel);

// ── Enemy ─────────────────────────────────────────────────────────────────────

/// <summary>An enemy entity that players can encounter in zones.</summary>
public record EnemyDto(
    string Slug,
    string Name,
    int Health,
    int Level,
    string Family,
    Dictionary<string, int> Attributes);

// ── NPC ───────────────────────────────────────────────────────────────────────

/// <summary>A non-player character present in zones (vendors, quest-givers, etc.).</summary>
public record NpcDto(
    string Slug,
    string Name,
    string DisplayName,
    string Category);

// ── Quest ─────────────────────────────────────────────────────────────────────

/// <summary>A quest available for player characters to accept and complete.</summary>
public record QuestDto(
    string Slug,
    string Title,
    string DisplayName,
    string QuestType,
    string Difficulty,
    int RarityWeight,
    string Description);

// ── Recipe ────────────────────────────────────────────────────────────────────

/// <summary>A single material ingredient used in a crafting <see cref="RecipeDto"/>.</summary>
public record RecipeMaterialDto(
    string ItemReference,
    int Quantity);

/// <summary>A crafting recipe that produces an item from a set of materials.</summary>
public record RecipeDto(
    string Slug,
    string Name,
    string Category,
    int RequiredLevel,
    string RequiredStation,
    List<RecipeMaterialDto> Materials,
    string OutputItemReference,
    int OutputQuantity);

// ── LootTable ─────────────────────────────────────────────────────────────────

/// <summary>A single entry in a loot table, describing one possible item drop.</summary>
public record LootTableEntryDto(
    string ItemDomain,
    string ItemSlug,
    int DropWeight,
    int QuantityMin,
    int QuantityMax,
    bool IsGuaranteed);

/// <summary>A loot table attached to an enemy, chest, or harvesting node.</summary>
public record LootTableDto(
    string Slug,
    string Name,
    string Context,
    bool IsBoss,
    bool IsChest,
    bool IsHarvesting,
    List<LootTableEntryDto> Entries);

// ── Spell ─────────────────────────────────────────────────────────────────────

/// <summary>A castable spell belonging to a particular school of magic.</summary>
public record SpellDto(
    string SpellId,
    string Name,
    string DisplayName,
    string School,
    int Rank,
    int ManaCost);

// ── ActorClass ────────────────────────────────────────────────────────────────

/// <summary>A playable character class (e.g. Fighter, Wizard) with its core mechanical properties.</summary>
/// <param name="Slug">Unique URL-safe identifier for this class.</param>
/// <param name="DisplayName">Display name shown to players (e.g. "Warrior", "Mage").</param>
/// <param name="TypeKey">DB category key for this class family (e.g. "warriors", "casters"). Not a content-reference prefix.</param>
/// <param name="HitDie">Sides of the health die rolled per level (e.g. 10 for Fighter, 6 for Wizard).</param>
/// <param name="PrimaryStat">Primary scaling attribute (e.g. "strength", "intelligence").</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record ActorClassDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int HitDie,
    string PrimaryStat,
    int RarityWeight);

// ── Species ───────────────────────────────────────────────────────────────────

/// <summary>A playable species (e.g. Human, Elf) selectable during character creation.</summary>
public record SpeciesDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Background ────────────────────────────────────────────────────────────────

/// <summary>A character background (e.g. Soldier, Sage) that provides starting bonuses.</summary>
public record BackgroundDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Skill ─────────────────────────────────────────────────────────────────────

/// <summary>A learnable skill that grants bonuses as its rank increases.</summary>
public record SkillDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int MaxRank,
    string? GoverningAttribute,
    int RarityWeight);

// ── Item ──────────────────────────────────────────────────────────────────────

/// <summary>A general-purpose catalog item (consumable, gem, rune, essence, etc.).</summary>
/// <param name="Slug">Unique URL-safe identifier for this item.</param>
/// <param name="DisplayName">Display name shown to players.</param>
/// <param name="TypeKey">Category key grouping related items (e.g. "consumables", "gems").</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record ItemDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Enchantment ───────────────────────────────────────────────────────────────

/// <summary>An enchantment that can be applied to equipment to grant bonuses.</summary>
/// <param name="Slug">Unique URL-safe identifier for this enchantment.</param>
/// <param name="DisplayName">Display name shown to players.</param>
/// <param name="TypeKey">Category key grouping related enchantments.</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record EnchantmentDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Weapon ────────────────────────────────────────────────────────────────────

/// <summary>A weapon catalog entry with its combat category and weapon sub-type.</summary>
/// <param name="Slug">Unique URL-safe identifier for this weapon.</param>
/// <param name="DisplayName">Display name shown to players.</param>
/// <param name="TypeKey">DB category key grouping related weapons (e.g. "swords", "axes", "bows").</param>
/// <param name="WeaponType">Sub-type of the weapon (e.g. "sword", "axe", "bow").</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record WeaponDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    string WeaponType,
    int RarityWeight);

// ── Armor ─────────────────────────────────────────────────────────────────────

/// <summary>An armor catalog entry with its protection class and equip slot.</summary>
/// <param name="Slug">Unique URL-safe identifier for this armor piece.</param>
/// <param name="DisplayName">Display name shown to players.</param>
/// <param name="TypeKey">DB category key grouping related armor pieces (e.g. "chest", "helm", "boots").</param>
/// <param name="ArmorType">Protection class of the armor (e.g. "light", "medium", "heavy", "shield").</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record ArmorDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    string ArmorType,
    int RarityWeight);

// ── Material ──────────────────────────────────────────────────────────────────

/// <summary>A crafting material belonging to a material family (metals, woods, leathers, etc.).</summary>
/// <param name="Slug">Unique URL-safe identifier for this material.</param>
/// <param name="DisplayName">Display name shown to players.</param>
/// <param name="MaterialFamily">Top-level grouping of the material (e.g. "metals", "woods", "leathers").</param>
/// <param name="RarityWeight">Rarity weight for procedural selection — lower values are more common.</param>
public record MaterialDto(
    string Slug,
    string DisplayName,
    string MaterialFamily,
    int RarityWeight);

// ── Organization ──────────────────────────────────────────────────────────────
/// <summary>An organization catalog entry (faction, guild, business, or shop).</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Domain type key (e.g. "factions", "guilds").</param>
/// <param name="OrgType">Org sub-type: "faction", "guild", "business", or "shop".</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
public record OrganizationDto(string Slug, string DisplayName, string TypeKey, string OrgType, int RarityWeight);

// ── WorldLocation ─────────────────────────────────────────────────────────────
/// <summary>A world location catalog entry (environment, location, or region).</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Domain type key (e.g. "environments", "locations", "regions").</param>
/// <param name="LocationType">Location sub-type: "environment", "location", or "region".</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
/// <param name="MinLevel">Minimum recommended character level, or <see langword="null"/> if unconstrained.</param>
/// <param name="MaxLevel">Maximum recommended character level, or <see langword="null"/> if unconstrained.</param>
public record WorldLocationDto(string Slug, string DisplayName, string TypeKey,
    string LocationType, int RarityWeight, int? MinLevel, int? MaxLevel);

// ── Dialogue ──────────────────────────────────────────────────────────────────
/// <summary>A dialogue catalog entry belonging to a speaker type.</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Domain type key (e.g. "greetings", "farewells").</param>
/// <param name="Speaker">Speaker type (e.g. "merchant", "guard"), or <see langword="null"/> for any speaker.</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
/// <param name="Lines">Dialogue lines.</param>
public record DialogueDto(string Slug, string DisplayName, string TypeKey,
    string? Speaker, int RarityWeight, List<string> Lines);

// ── ActorInstance ─────────────────────────────────────────────────────────────
/// <summary>A named actor instance that overrides an archetype for quest-critical or unique actors.</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Origin category (e.g. "boss", "story", "unique").</param>
/// <param name="ArchetypeId">ID of the base actor archetype.</param>
/// <param name="LevelOverride">Level override, or <see langword="null"/> to use archetype default.</param>
/// <param name="FactionOverride">Faction override, or <see langword="null"/> to use archetype default.</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
public record ActorInstanceDto(string Slug, string DisplayName, string TypeKey,
    Guid ArchetypeId, int? LevelOverride, string? FactionOverride, int RarityWeight);

// ── MaterialProperty ──────────────────────────────────────────────────────────
/// <summary>A material property definition describing the physical characteristics of a material family.</summary>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="TypeKey">Domain type key (e.g. "metals", "woods").</param>
/// <param name="MaterialFamily">Material family: "metal", "wood", "leather", "gem", "fabric", "bone", or "stone".</param>
/// <param name="CostScale">Budget formula multiplier.</param>
/// <param name="RarityWeight">Selection weight for random draws.</param>
public record MaterialPropertyDto(string Slug, string DisplayName, string TypeKey,
    string MaterialFamily, float CostScale, int RarityWeight);

// ── TraitDefinition ───────────────────────────────────────────────────────────
/// <summary>A trait vocabulary entry defining a known trait key and its value type.</summary>
/// <param name="Key">Trait key used in entity Traits columns (e.g. "aggressive", "fireResist").</param>
/// <param name="ValueType">Value type: "bool", "int", "float", or "string".</param>
/// <param name="Description">Human-readable description, or <see langword="null"/>.</param>
/// <param name="AppliesTo">Comma-separated entity types, or "*" for all, or <see langword="null"/>.</param>
public record TraitDefinitionDto(string Key, string ValueType, string? Description, string? AppliesTo);
