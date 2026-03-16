using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Foundry;
using SharedAbility = RealmEngine.Shared.Models.Ability;
using SharedQuest   = RealmEngine.Shared.Models.Quest;
using SharedRecipe  = RealmEngine.Shared.Models.Recipe;
using SharedSpell   = RealmEngine.Shared.Models.Spell;

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
/// GET /api/content/spells/{slug}          — single spell
/// GET /api/content/classes               — all active actor classes
/// GET /api/content/classes/{slug}        — single actor class
/// GET /api/content/species               — all active species
/// GET /api/content/species/{slug}        — single species
/// GET /api/content/backgrounds           — all active backgrounds
/// GET /api/content/backgrounds/{slug}    — single background
/// GET /api/content/skills                — all active skills
/// GET /api/content/skills/{slug}         — single skill
///
/// GET /api/content/schema                — all registered content type schemas (public)
/// GET /api/content/browse                — paged list across any entity type (public)
/// GET /api/content/browse/{type}/{slug}  — full entity detail by type + slug (public)
/// /// </summary>
public static class ContentEndpoints
{
    private static readonly JsonSerializerOptions _detailJsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content")
            .WithTags("Content")
            .AllowAnonymous();

        // ── Public schema + browse (no auth, no player session required) ───────
        group.MapGet("/schema",                   GetSchemaAsync);
        group.MapGet("/browse",                   BrowseAsync);
        group.MapGet("/browse/{type}/{slug}",     BrowseDetailAsync);

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

        // Actor classes
        group.MapGet("/classes",          GetClassesAsync);
        group.MapGet("/classes/{slug}",   GetClassBySlugAsync);

        // Species
        group.MapGet("/species",          GetSpeciesAsync);
        group.MapGet("/species/{slug}",   GetSpeciesBySlugAsync);

        // Backgrounds
        group.MapGet("/backgrounds",          GetBackgroundsAsync);
        group.MapGet("/backgrounds/{slug}",   GetBackgroundBySlugAsync);

        // Skills
        group.MapGet("/skills",           GetSkillsAsync);
        group.MapGet("/skills/{slug}",    GetSkillBySlugAsync);

        return app;
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private static IResult GetSchemaAsync() =>
        Results.Ok(ContentSchemaRegistry.All.Values.Select(s =>
            new ContentTypeInfoDto(s.ContentType, s.DisplayLabel, s.Description)));

    // ── Generic browse (paged summary list) ───────────────────────────────────

    private static async Task<IResult> BrowseAsync(
        string type,
        string? search,
        int page,
        int pageSize,
        ContentDbContext db,
        CancellationToken ct)
    {
        page     = Math.Max(1, page == 0 ? 1 : page);
        pageSize = Math.Clamp(pageSize == 0 ? 20 : pageSize, 1, 100);

        var result = type.ToLowerInvariant() switch
        {
            "ability"          => await BrowseSet(db.Abilities,          type, search, page, pageSize, ct),
            "species"          => await BrowseSet(db.Species,            type, search, page, pageSize, ct),
            "class"            => await BrowseSet(db.ActorClasses,       type, search, page, pageSize, ct),
            "archetype"        => await BrowseSet(db.ActorArchetypes,    type, search, page, pageSize, ct),
            "instance"         => await BrowseSet(db.ActorInstances,     type, search, page, pageSize, ct),
            "background"       => await BrowseSet(db.Backgrounds,        type, search, page, pageSize, ct),
            "skill"            => await BrowseSet(db.Skills,             type, search, page, pageSize, ct),
            "weapon"           => await BrowseSet(db.Weapons,            type, search, page, pageSize, ct),
            "armor"            => await BrowseSet(db.Armors,             type, search, page, pageSize, ct),
            "item"             => await BrowseSet(db.Items,              type, search, page, pageSize, ct),
            "material"         => await BrowseSet(db.Materials,          type, search, page, pageSize, ct),
            "materialproperty" => await BrowseSet(db.MaterialProperties, type, search, page, pageSize, ct),
            "enchantment"      => await BrowseSet(db.Enchantments,       type, search, page, pageSize, ct),
            "spell"            => await BrowseSet(db.Spells,             type, search, page, pageSize, ct),
            "quest"            => await BrowseSet(db.Quests,             type, search, page, pageSize, ct),
            "recipe"           => await BrowseSet(db.Recipes,            type, search, page, pageSize, ct),
            "loottable"        => await BrowseSet(db.LootTables,         type, search, page, pageSize, ct),
            "organization"     => await BrowseSet(db.Organizations,      type, search, page, pageSize, ct),
            "worldlocation"    => await BrowseSet(db.WorldLocations,     type, search, page, pageSize, ct),
            "dialogue"         => await BrowseSet(db.Dialogues,          type, search, page, pageSize, ct),
            _                  => null,
        };

        return result is null
            ? Results.NotFound(new { error = $"Unknown content type: {type}" })
            : Results.Ok(result);
    }

    private static async Task<PagedResult<ContentSummaryDto>> BrowseSet<T>(
        IQueryable<T> set, string contentType,
        string? search, int page, int pageSize, CancellationToken ct)
        where T : ContentBase
    {
        var query = set.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x =>
                EF.Functions.ILike(x.Slug, $"%{search}%") ||
                (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{search}%")));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.DisplayName ?? x.Slug)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ContentSummaryDto(
                x.Id, x.Slug, x.DisplayName, x.TypeKey, contentType,
                x.RarityWeight, x.IsActive, x.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<ContentSummaryDto>(items, total, page, pageSize);
    }

    // ── Generic browse (full entity detail) ───────────────────────────────────

    private static async Task<IResult> BrowseDetailAsync(
        string type,
        string slug,
        ContentDbContext db,
        CancellationToken ct)
    {
        static ContentSummaryDto Summary(ContentBase e, string t) =>
            new(e.Id, e.Slug, e.DisplayName, e.TypeKey, t, e.RarityWeight, e.IsActive, e.UpdatedAt);

        ContentBase? entity = type.ToLowerInvariant() switch
        {
            "ability"          => await db.Abilities.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "species"          => await db.Species.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "class"            => await db.ActorClasses.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "archetype"        => await db.ActorArchetypes.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "instance"         => await db.ActorInstances.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "background"       => await db.Backgrounds.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "skill"            => await db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "weapon"           => await db.Weapons.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "armor"            => await db.Armors.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "item"             => await db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "material"         => await db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "materialproperty" => await db.MaterialProperties.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "enchantment"      => await db.Enchantments.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "spell"            => await db.Spells.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "quest"            => await db.Quests.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "recipe"           => await db.Recipes.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "loottable"        => await db.LootTables.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "organization"     => await db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "worldlocation"    => await db.WorldLocations.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "dialogue"         => await db.Dialogues.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            _                  => null,
        };

        if (entity is null) return Results.NotFound();

        var payload = JsonSerializer.SerializeToElement(entity, entity.GetType(), _detailJsonOpts);
        return Results.Ok(new ContentDetailDto(Summary(entity, type), payload));
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

    private static AbilityDto ToDto(SharedAbility a) => new(
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

    private static QuestDto ToDto(SharedQuest q) => new(
        Slug:         q.Slug,
        Title:        q.Title,
        DisplayName:  q.DisplayName,
        QuestType:    q.QuestType,
        Difficulty:   q.Difficulty,
        RarityWeight: q.RarityWeight,
        Description:  q.Description);

    private static RecipeDto ToDto(SharedRecipe r) => new(
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

    private static SpellDto ToDto(SharedSpell s) => new(
        SpellId:     s.SpellId,
        Name:        s.Name,
        DisplayName: s.DisplayName,
        School:      s.Tradition.ToString(),
        Rank:        s.Rank,
        ManaCost:    s.ManaCost);

    // ── Actor Classes ─────────────────────────────────────────────────────────

    private static async Task<IResult> GetClassesAsync(ContentDbContext db)
    {
        var items = await db.ActorClasses
            .Where(c => c.IsActive)
            .AsNoTracking()
            .ToListAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetClassBySlugAsync(string slug, ContentDbContext db)
    {
        var item = await db.ActorClasses
            .Where(c => c.IsActive && c.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Species ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSpeciesAsync(ContentDbContext db)
    {
        var items = await db.Species
            .Where(s => s.IsActive)
            .AsNoTracking()
            .ToListAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetSpeciesBySlugAsync(string slug, ContentDbContext db)
    {
        var item = await db.Species
            .Where(s => s.IsActive && s.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Backgrounds ───────────────────────────────────────────────────────────

    private static async Task<IResult> GetBackgroundsAsync(ContentDbContext db)
    {
        var items = await db.Backgrounds
            .Where(b => b.IsActive)
            .AsNoTracking()
            .ToListAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetBackgroundBySlugAsync(string slug, ContentDbContext db)
    {
        var item = await db.Backgrounds
            .Where(b => b.IsActive && b.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSkillsAsync(ContentDbContext db)
    {
        var items = await db.Skills
            .Where(s => s.IsActive)
            .AsNoTracking()
            .ToListAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetSkillBySlugAsync(string slug, ContentDbContext db)
    {
        var item = await db.Skills
            .Where(s => s.IsActive && s.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    // ── Mapping helpers (new content types) ───────────────────────────────────

    private static ActorClassDto ToDto(RealmEngine.Data.Entities.ActorClass c) => new(
        Slug:        c.Slug,
        DisplayName: c.DisplayName ?? c.Slug,
        TypeKey:     c.TypeKey,
        HitDie:      c.HitDie,
        PrimaryStat: c.PrimaryStat,
        RarityWeight: c.RarityWeight);

    private static SpeciesDto ToDto(RealmEngine.Data.Entities.Species s) => new(
        Slug:        s.Slug,
        DisplayName: s.DisplayName ?? s.Slug,
        TypeKey:     s.TypeKey,
        RarityWeight: s.RarityWeight);

    private static BackgroundDto ToDto(RealmEngine.Data.Entities.Background b) => new(
        Slug:        b.Slug,
        DisplayName: b.DisplayName ?? b.Slug,
        TypeKey:     b.TypeKey,
        RarityWeight: b.RarityWeight);

    private static SkillDto ToDto(RealmEngine.Data.Entities.Skill s) => new(
        Slug:               s.Slug,
        DisplayName:        s.DisplayName ?? s.Slug,
        TypeKey:            s.TypeKey,
        MaxRank:            s.MaxRank,
        GoverningAttribute: s.GoverningAttribute,
        RarityWeight:       s.RarityWeight);
}
