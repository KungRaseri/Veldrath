using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Foundry;
using SharedPower       = RealmEngine.Shared.Models.Power;
using SharedQuest       = RealmEngine.Shared.Models.Quest;
using SharedRecipe      = RealmEngine.Shared.Models.Recipe;
using DataItem          = RealmEngine.Data.Entities.Item;

namespace RealmUnbound.Server.Features.Content;

/// <summary>
/// Read-only catalog endpoints for game content data.
/// Returns pre-mapped contract DTOs from the content repositories.
///
/// GET /api/content/powers                 — all active powers
/// GET /api/content/powers/{slug}           — single power
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
/// GET /api/content/skills/{slug}         — single skill/// GET /api/content/items                  — all active catalog items; optional ?type= filter (e.g. ?type=weapon, ?type=armor)
/// GET /api/content/items/{slug}           — single catalog item by slug
/// GET /api/content/enchantments           — all active enchantments; optional ?targetSlot= filter
/// GET /api/content/enchantments/{slug}    — single enchantment by slug
/// GET /api/content/materials              — all active materials
/// GET /api/content/materials/{slug}       — single material by slug
///
/// GET /api/content/schema                — all registered content type schemas (public)
/// GET /api/content/browse                — paged list across any entity type (public)
/// GET /api/content/browse/{type}/{slug}  — full entity detail by type + slug (public)
/// </summary>
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

        // Powers
        group.MapGet("/powers",        GetPowersAsync);
        group.MapGet("/powers/{slug}", GetPowerBySlugAsync);

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

        // Powers replace the former /spells and /abilities endpoints

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

        // Items
        group.MapGet("/items",            GetItemsAsync);
        group.MapGet("/items/{slug}",     GetItemBySlugAsync);

        // Enchantments
        group.MapGet("/enchantments",         GetEnchantmentsAsync);
        group.MapGet("/enchantments/{slug}",  GetEnchantmentBySlugAsync);

        // Materials
        group.MapGet("/materials",        GetMaterialsAsync);
        group.MapGet("/materials/{slug}", GetMaterialBySlugAsync);

        // Organizations
        group.MapGet("/organizations",          GetOrganizationsAsync);
        group.MapGet("/organizations/{slug}",   GetOrganizationBySlugAsync);

        // World Locations
        group.MapGet("/world-locations",          GetWorldLocationsAsync);
        group.MapGet("/world-locations/{slug}",   GetWorldLocationBySlugAsync);

        // Dialogues
        group.MapGet("/dialogues",          GetDialoguesAsync);
        group.MapGet("/dialogues/{slug}",   GetDialogueBySlugAsync);

        // Actor Instances
        group.MapGet("/actor-instances",          GetActorInstancesAsync);
        group.MapGet("/actor-instances/{slug}",   GetActorInstanceBySlugAsync);

        // Material Properties
        group.MapGet("/material-properties",          GetMaterialPropertiesAsync);
        group.MapGet("/material-properties/{slug}",   GetMaterialPropertyBySlugAsync);

        // Trait Definitions
        group.MapGet("/traits",         GetTraitsAsync);
        group.MapGet("/traits/{key}",   GetTraitByKeyAsync);

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
        int? page,
        int? pageSize,
        ContentDbContext db,
        CancellationToken ct)
    {
        var resolvedPage     = Math.Max(1, page ?? 1);
        var resolvedPageSize = Math.Clamp(pageSize ?? 20, 1, 100);

        var result = type.ToLowerInvariant() switch
        {
            "power"            => await BrowseSet(db.Powers,             type, search, resolvedPage, resolvedPageSize, ct),
            "ability"          => await BrowseSet(db.Powers,             type, search, resolvedPage, resolvedPageSize, ct),
            "species"          => await BrowseSet(db.Species,            type, search, resolvedPage, resolvedPageSize, ct),
            "class"            => await BrowseSet(db.ActorClasses,       type, search, resolvedPage, resolvedPageSize, ct),
            "archetype"        => await BrowseSet(db.ActorArchetypes,    type, search, resolvedPage, resolvedPageSize, ct),
            "instance"         => await BrowseSet(db.ActorInstances,     type, search, resolvedPage, resolvedPageSize, ct),
            "background"       => await BrowseSet(db.Backgrounds,        type, search, resolvedPage, resolvedPageSize, ct),
            "skill"            => await BrowseSet(db.Skills,             type, search, resolvedPage, resolvedPageSize, ct),
            "item"             => await BrowseSet(db.Items,              type, search, resolvedPage, resolvedPageSize, ct),
            "material"         => await BrowseSet(db.Materials,          type, search, resolvedPage, resolvedPageSize, ct),
            "materialproperty" => await BrowseSet(db.MaterialProperties, type, search, resolvedPage, resolvedPageSize, ct),
            "enchantment"      => await BrowseSet(db.Enchantments,       type, search, resolvedPage, resolvedPageSize, ct),
            "spell"            => await BrowseSet(db.Powers,             type, search, resolvedPage, resolvedPageSize, ct),
            "quest"            => await BrowseSet(db.Quests,             type, search, resolvedPage, resolvedPageSize, ct),
            "recipe"           => await BrowseSet(db.Recipes,            type, search, resolvedPage, resolvedPageSize, ct),
            "loottable"        => await BrowseSet(db.LootTables,         type, search, resolvedPage, resolvedPageSize, ct),
            "organization"     => await BrowseSet(db.Organizations,      type, search, resolvedPage, resolvedPageSize, ct),
            "worldlocation"    => await BrowseSet(db.WorldLocations,     type, search, resolvedPage, resolvedPageSize, ct),
            "dialogue"         => await BrowseSet(db.Dialogues,          type, search, resolvedPage, resolvedPageSize, ct),
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
            "power"            => await db.Powers.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "ability"          => await db.Powers.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "species"          => await db.Species.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "class"            => await db.ActorClasses.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "archetype"        => await db.ActorArchetypes.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "instance"         => await db.ActorInstances.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "background"       => await db.Backgrounds.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "skill"            => await db.Skills.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "item"             => await db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "material"         => await db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "materialproperty" => await db.MaterialProperties.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "enchantment"      => await db.Enchantments.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
            "spell"            => await db.Powers.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Slug == slug, ct),
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

    // ── Powers ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetPowersAsync(IPowerRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> GetPowerBySlugAsync(string slug, IPowerRepository repo)
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

    // ── Spells section removed — merged into /powers ──────────────────────────

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static PowerDto ToDto(SharedPower p) => new(
        Slug:          p.Slug,
        DisplayName:   p.DisplayName,
        PowerType:     p.Type.ToString(),
        School:        p.School,
        Description:   p.Description,
        ManaCost:      p.ManaCost,
        Cooldown:      p.Cooldown,
        Range:         p.Range ?? 0,
        RarityWeight:  p.RarityWeight,
        IsPassive:     p.IsPassive,
        RequiredLevel: p.RequiredLevel,
        Rank:          p.Rank,
        EffectType:    p.EffectType.ToString());

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

    // ── Items ──────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetItemsAsync(string? type, ContentDbContext db, CancellationToken ct)
    {
        var query = db.Items.Where(i => i.IsActive).AsNoTracking();
        if (type is not null)
            query = query.Where(i => i.ItemType == type.ToLowerInvariant());
        var items = await query.ToListAsync(ct);
        return Results.Ok(items.Select(ToItemDto));
    }

    private static async Task<IResult> GetItemBySlugAsync(string slug, ContentDbContext db, CancellationToken ct)
    {
        var item = await db.Items
            .Where(i => i.IsActive && i.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(ToItemDto(item));
    }

    // ── Enchantments ───────────────────────────────────────────────────────────

    private static async Task<IResult> GetEnchantmentsAsync(string? targetSlot, ContentDbContext db, CancellationToken ct)
    {
        var query = db.Enchantments.Where(e => e.IsActive).AsNoTracking();
        if (targetSlot is not null)
            query = query.Where(e => e.TargetSlot == targetSlot.ToLowerInvariant());
        var items = await query.ToListAsync(ct);
        return Results.Ok(items.Select(ToEnchantmentDto));
    }

    private static async Task<IResult> GetEnchantmentBySlugAsync(string slug, ContentDbContext db, CancellationToken ct)
    {
        var item = await db.Enchantments
            .Where(e => e.IsActive && e.Slug == slug)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(ToEnchantmentDto(item));
    }

    private static ItemDto ToItemDto(DataItem i) => new(
        Slug:        i.Slug,
        DisplayName: i.DisplayName ?? i.Slug,
        TypeKey:     i.TypeKey,
        RarityWeight: i.RarityWeight,
        ItemType:    i.ItemType,
        WeaponType:  i.WeaponType,
        ArmorType:   i.ArmorType);

    private static EnchantmentDto ToEnchantmentDto(RealmEngine.Data.Entities.Enchantment e) => new(
        Slug:         e.Slug,
        DisplayName:  e.DisplayName ?? e.Slug,
        TypeKey:      e.TypeKey,
        RarityWeight: e.RarityWeight);

    // ── Materials ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetMaterialsAsync(IMaterialRepository repo)
    {
        var items = await repo.GetAllAsync();
        return Results.Ok(items.Select(ToMaterialDto));
    }

    private static async Task<IResult> GetMaterialBySlugAsync(string slug, IMaterialRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToMaterialDto(item));
    }

    private static MaterialDto ToMaterialDto(MaterialEntry m) => new(
        Slug:           m.Slug,
        DisplayName:    m.DisplayName,
        MaterialFamily: m.MaterialFamily,
        RarityWeight:   (int)m.RarityWeight);

    // ── Organizations ─────────────────────────────────────────────────────────

    private static async Task<IResult> GetOrganizationsAsync(IOrganizationRepository repo, string? orgType = null)
    {
        var items = orgType is not null
            ? await repo.GetByTypeAsync(orgType)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToOrganizationDto));
    }

    private static async Task<IResult> GetOrganizationBySlugAsync(string slug, IOrganizationRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToOrganizationDto(item));
    }

    private static OrganizationDto ToOrganizationDto(OrganizationEntry o) =>
        new(o.Slug, o.DisplayName, o.TypeKey, o.OrgType, o.RarityWeight);

    // ── World Locations ───────────────────────────────────────────────────────

    private static async Task<IResult> GetWorldLocationsAsync(IWorldLocationRepository repo, string? locationType = null)
    {
        var items = locationType is not null
            ? await repo.GetByLocationTypeAsync(locationType)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToWorldLocationDto));
    }

    private static async Task<IResult> GetWorldLocationBySlugAsync(string slug, IWorldLocationRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToWorldLocationDto(item));
    }

    private static WorldLocationDto ToWorldLocationDto(WorldLocationEntry w) =>
        new(w.Slug, w.DisplayName, w.TypeKey, w.LocationType, w.RarityWeight, w.MinLevel, w.MaxLevel);

    // ── Dialogues ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetDialoguesAsync(IDialogueRepository repo, string? speaker = null)
    {
        var items = speaker is not null
            ? await repo.GetBySpeakerAsync(speaker)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToDialogueDto));
    }

    private static async Task<IResult> GetDialogueBySlugAsync(string slug, IDialogueRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToDialogueDto(item));
    }

    private static DialogueDto ToDialogueDto(DialogueEntry d) =>
        new(d.Slug, d.DisplayName, d.TypeKey, d.Speaker, d.RarityWeight, d.Lines);

    // ── Actor Instances ───────────────────────────────────────────────────────

    private static async Task<IResult> GetActorInstancesAsync(IActorInstanceRepository repo, string? typeKey = null)
    {
        var items = typeKey is not null
            ? await repo.GetByTypeKeyAsync(typeKey)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToActorInstanceDto));
    }

    private static async Task<IResult> GetActorInstanceBySlugAsync(string slug, IActorInstanceRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToActorInstanceDto(item));
    }

    private static ActorInstanceDto ToActorInstanceDto(ActorInstanceEntry a) =>
        new(a.Slug, a.DisplayName, a.TypeKey, a.ArchetypeId, a.LevelOverride, a.FactionOverride, a.RarityWeight);

    // ── Material Properties ───────────────────────────────────────────────────

    private static async Task<IResult> GetMaterialPropertiesAsync(IMaterialPropertyRepository repo, string? family = null)
    {
        var items = family is not null
            ? await repo.GetByFamilyAsync(family)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToMaterialPropertyDto));
    }

    private static async Task<IResult> GetMaterialPropertyBySlugAsync(string slug, IMaterialPropertyRepository repo)
    {
        var item = await repo.GetBySlugAsync(slug);
        return item is null ? Results.NotFound() : Results.Ok(ToMaterialPropertyDto(item));
    }

    private static MaterialPropertyDto ToMaterialPropertyDto(MaterialPropertyEntry m) =>
        new(m.Slug, m.DisplayName, m.TypeKey, m.MaterialFamily, m.CostScale, m.RarityWeight);

    // ── Trait Definitions ─────────────────────────────────────────────────────

    private static async Task<IResult> GetTraitsAsync(ITraitDefinitionRepository repo, string? appliesTo = null)
    {
        var items = appliesTo is not null
            ? await repo.GetByAppliesToAsync(appliesTo)
            : await repo.GetAllAsync();
        return Results.Ok(items.Select(ToTraitDefinitionDto));
    }

    private static async Task<IResult> GetTraitByKeyAsync(string key, ITraitDefinitionRepository repo)
    {
        var item = await repo.GetByKeyAsync(key);
        return item is null ? Results.NotFound() : Results.Ok(ToTraitDefinitionDto(item));
    }

    private static TraitDefinitionDto ToTraitDefinitionDto(TraitDefinitionEntry t) =>
        new(t.Key, t.ValueType, t.Description, t.AppliesTo);
}
