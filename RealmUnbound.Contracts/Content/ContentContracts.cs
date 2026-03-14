namespace RealmUnbound.Contracts.Content;

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
