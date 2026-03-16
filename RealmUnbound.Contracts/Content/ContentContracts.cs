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

public record EnemyDto(
    string Slug,
    string Name,
    int Health,
    int Level,
    string Family,
    Dictionary<string, int> Attributes);

// ── NPC ───────────────────────────────────────────────────────────────────────

public record NpcDto(
    string Slug,
    string Name,
    string DisplayName,
    string Category);

// ── Quest ─────────────────────────────────────────────────────────────────────

public record QuestDto(
    string Slug,
    string Title,
    string DisplayName,
    string QuestType,
    string Difficulty,
    int RarityWeight,
    string Description);

// ── Recipe ────────────────────────────────────────────────────────────────────

public record RecipeMaterialDto(
    string ItemReference,
    int Quantity);

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

public record LootTableEntryDto(
    string ItemDomain,
    string ItemSlug,
    int DropWeight,
    int QuantityMin,
    int QuantityMax,
    bool IsGuaranteed);

public record LootTableDto(
    string Slug,
    string Name,
    string Context,
    bool IsBoss,
    bool IsChest,
    bool IsHarvesting,
    List<LootTableEntryDto> Entries);

// ── Spell ─────────────────────────────────────────────────────────────────────

public record SpellDto(
    string SpellId,
    string Name,
    string DisplayName,
    string School,
    int Rank,
    int ManaCost);

// ── ActorClass ────────────────────────────────────────────────────────────────

public record ActorClassDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int HitDie,
    string PrimaryStat,
    int RarityWeight);

// ── Species ───────────────────────────────────────────────────────────────────

public record SpeciesDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Background ────────────────────────────────────────────────────────────────

public record BackgroundDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int RarityWeight);

// ── Skill ─────────────────────────────────────────────────────────────────────

public record SkillDto(
    string Slug,
    string DisplayName,
    string TypeKey,
    int MaxRank,
    string? GoverningAttribute,
    int RarityWeight);
