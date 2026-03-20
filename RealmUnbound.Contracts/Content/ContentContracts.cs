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
