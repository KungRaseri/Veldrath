using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Server.Features.Content;

/// <summary>
/// Read-only catalog endpoints for game content data.
/// Returns pre-mapped contract DTOs from the content repositories.
///
/// GET /api/content/abilities             — all active abilities
/// GET /api/content/abilities/{slug}      — single ability
/// GET /api/content/enemies               — all active enemies
/// GET /api/content/enemies/{slug}        — single enemy
/// GET /api/content/npcs                  — all active NPCs
/// GET /api/content/npcs/{slug}           — single NPC
/// GET /api/content/quests                — all active quests
/// GET /api/content/quests/{slug}         — single quest
/// GET /api/content/recipes               — all active recipes
/// GET /api/content/recipes/{slug}        — single recipe
/// GET /api/content/loot-tables           — all active loot tables
/// GET /api/content/loot-tables/{slug}    — single loot table
/// GET /api/content/spells                — all active spells
/// GET /api/content/spells/{slug}         — single spell
/// </summary>
public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content")
            .WithTags("Content")
            .RequireAuthorization();

        // Abilities
        group.MapGet("/abilities",        GetAbilitiesAsync);
        group.MapGet("/abilities/{slug}", GetAbilityBySlugAsync);

        // Enemies
        group.MapGet("/enemies",          GetEnemiesAsync);
        group.MapGet("/enemies/{slug}",   GetEnemyBySlugAsync);

        // NPCs
        group.MapGet("/npcs",             GetNpcsAsync);
        group.MapGet("/npcs/{slug}",      GetNpcBySlugAsync);

        // Quests
        group.MapGet("/quests",           GetQuestsAsync);
        group.MapGet("/quests/{slug}",    GetQuestBySlugAsync);

        // Recipes
        group.MapGet("/recipes",          GetRecipesAsync);
        group.MapGet("/recipes/{slug}",   GetRecipeBySlugAsync);

        // Loot tables
        group.MapGet("/loot-tables",           GetLootTablesAsync);
        group.MapGet("/loot-tables/{slug}",    GetLootTableBySlugAsync);

        // Spells
        group.MapGet("/spells",           GetSpellsAsync);
        group.MapGet("/spells/{slug}",    GetSpellBySlugAsync);

        return app;
    }

    // ── Abilities ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetAbilitiesAsync(IAbilityRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetAbilityBySlugAsync(string slug, IAbilityRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Enemies ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetEnemiesAsync(IEnemyRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetEnemyBySlugAsync(string slug, IEnemyRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── NPCs ──────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetNpcsAsync(INpcRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetNpcBySlugAsync(string slug, INpcRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Quests ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetQuestsAsync(IQuestRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetQuestBySlugAsync(string slug, IQuestRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Recipes ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetRecipesAsync(IRecipeRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetRecipeBySlugAsync(string slug, IRecipeRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Loot Tables ───────────────────────────────────────────────────────────

    private static async Task<IResult> GetLootTablesAsync(ILootTableRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetLootTableBySlugAsync(string slug, ILootTableRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Spells ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSpellsAsync(ISpellRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetSpellBySlugAsync(string slug, ISpellRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static AbilityDto ToDto(Ability a) => new(
        Slug:          a.Slug,
        DisplayName:   a.DisplayName,
        AbilityType:   a.Type.ToString(),
        Description:   a.Description,
        ManaCost:      a.ManaCost,
        Cooldown:      a.Cooldown,
        Range:         a.Range ?? 0,
        RarityWeight:  a.RarityWeight,
        IsPassive:     a.IsPassive,
        RequiredLevel: a.RequiredLevel);

    private static EnemyDto ToDto(Enemy e) => new(
        Slug:       e.Slug,
        Name:       e.Name,
        Health:     e.Health,
        Level:      e.Level,
        Family:     e.BaseName,
        Attributes: e.Attributes);

    private static NpcDto ToDto(NPC n) => new(
        Slug:        n.Slug,
        Name:        n.Name,
        DisplayName: n.DisplayName,
        Category:    n.Occupation);

    private static QuestDto ToDto(Quest q) => new(
        Slug:         q.Slug,
        Title:        q.Title,
        DisplayName:  q.DisplayName,
        QuestType:    q.QuestType,
        Difficulty:   q.Difficulty,
        RarityWeight: q.RarityWeight,
        Description:  q.Description);

    private static RecipeDto ToDto(Recipe r) => new(
        Slug:                r.Slug,
        Name:                r.Name,
        Category:            r.Category,
        RequiredLevel:       r.RequiredLevel,
        RequiredStation:     r.RequiredStation,
        Materials:           r.Materials.Select(m => new RecipeMaterialDto(m.ItemReference, m.Quantity)).ToList(),
        OutputItemReference: r.OutputItemReference,
        OutputQuantity:      r.OutputQuantity);

    private static LootTableDto ToDto(LootTableData t) => new(
        Slug:         t.Slug,
        Name:         t.Name,
        Context:      t.Context,
        IsBoss:       t.IsBoss,
        IsChest:      t.IsChest,
        IsHarvesting: t.IsHarvesting,
        Entries:      t.Entries.Select(e => new LootTableEntryDto(
            e.ItemDomain, e.ItemSlug, e.DropWeight, e.QuantityMin, e.QuantityMax, e.IsGuaranteed)).ToList());

    private static SpellDto ToDto(Spell s) => new(
        SpellId:     s.SpellId,
        Name:        s.Name,
        DisplayName: s.DisplayName,
        School:      s.Tradition.ToString(),
        Rank:        s.Rank,
        ManaCost:    s.ManaCost);
}
