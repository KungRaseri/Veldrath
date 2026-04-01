using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Client-side in-memory cache for game content catalog data fetched from the server.
/// Lazy-loads each catalog on first request and holds it for the lifetime of the session.
/// Call <see cref="InvalidateAsync"/> to force a refresh (e.g., after server-side data updates).
/// </summary>
public class ContentCache(IContentService contentService, ILogger<ContentCache> logger)
{
    private List<PowerDto>?     _abilities;
    private List<EnemyDto>?       _enemies;
    private List<NpcDto>?         _npcs;
    private List<QuestDto>?       _quests;
    private List<RecipeDto>?      _recipes;
    private List<LootTableDto>?   _lootTables;
    private List<PowerDto>?       _spells;
    private List<ActorClassDto>?  _classes;
    private List<SpeciesDto>?     _species;
    private List<BackgroundDto>?  _backgrounds;
    private List<SkillDto>?       _skills;
    private List<ZoneLocationDto>? _zoneLocations;

    // Catalog accessors (lazy-load)
    public async Task<IReadOnlyList<PowerDto>> GetAbilitiesAsync()
    {
        if (_abilities is null)
        {
            logger.LogDebug("ContentCache: loading abilities from server");
            _abilities = await contentService.GetAbilitiesAsync();
        }
        return _abilities;
    }

    public async Task<IReadOnlyList<EnemyDto>> GetEnemiesAsync()
    {
        if (_enemies is null)
        {
            logger.LogDebug("ContentCache: loading enemies from server");
            _enemies = await contentService.GetEnemiesAsync();
        }
        return _enemies;
    }

    public async Task<IReadOnlyList<NpcDto>> GetNpcsAsync()
    {
        if (_npcs is null)
        {
            logger.LogDebug("ContentCache: loading NPCs from server");
            _npcs = await contentService.GetNpcsAsync();
        }
        return _npcs;
    }

    public async Task<IReadOnlyList<QuestDto>> GetQuestsAsync()
    {
        if (_quests is null)
        {
            logger.LogDebug("ContentCache: loading quests from server");
            _quests = await contentService.GetQuestsAsync();
        }
        return _quests;
    }

    public async Task<IReadOnlyList<RecipeDto>> GetRecipesAsync()
    {
        if (_recipes is null)
        {
            logger.LogDebug("ContentCache: loading recipes from server");
            _recipes = await contentService.GetRecipesAsync();
        }
        return _recipes;
    }

    public async Task<IReadOnlyList<LootTableDto>> GetLootTablesAsync()
    {
        if (_lootTables is null)
        {
            logger.LogDebug("ContentCache: loading loot tables from server");
            _lootTables = await contentService.GetLootTablesAsync();
        }
        return _lootTables;
    }

    public async Task<IReadOnlyList<PowerDto>> GetSpellsAsync()
    {
        if (_spells is null)
        {
            logger.LogDebug("ContentCache: loading spells from server");
            _spells = await contentService.GetSpellsAsync();
        }
        return _spells;
    }

    public async Task<IReadOnlyList<ActorClassDto>> GetClassesAsync()
    {
        if (_classes is null)
        {
            logger.LogDebug("ContentCache: loading classes from server");
            _classes = await contentService.GetClassesAsync();
        }
        return _classes;
    }

    public async Task<IReadOnlyList<SpeciesDto>> GetSpeciesAsync()
    {
        if (_species is null)
        {
            logger.LogDebug("ContentCache: loading species from server");
            _species = await contentService.GetSpeciesAsync();
        }
        return _species;
    }

    public async Task<IReadOnlyList<BackgroundDto>> GetBackgroundsAsync()
    {
        if (_backgrounds is null)
        {
            logger.LogDebug("ContentCache: loading backgrounds from server");
            _backgrounds = await contentService.GetBackgroundsAsync();
        }
        return _backgrounds;
    }

    public async Task<IReadOnlyList<SkillDto>> GetSkillsAsync()
    {
        if (_skills is null)
        {
            logger.LogDebug("ContentCache: loading skills from server");
            _skills = await contentService.GetSkillsAsync();
        }
        return _skills;
    }

    /// <summary>Gets the full list of zone locations, loading from the server on first call.</summary>
    public async Task<IReadOnlyList<ZoneLocationDto>> GetZoneLocationsAsync()
    {
        if (_zoneLocations is null)
        {
            logger.LogDebug("ContentCache: loading zone locations from server");
            _zoneLocations = await contentService.GetZoneLocationsAsync();
        }
        return _zoneLocations;
    }

    // Slug lookups
    public async Task<PowerDto?> GetAbilityAsync(string slug)
    {
        var list = await GetAbilitiesAsync();
        return list.FirstOrDefault(a => a.Slug == slug)
               ?? await contentService.GetAbilityAsync(slug);
    }

    public async Task<EnemyDto?> GetEnemyAsync(string slug)
    {
        var list = await GetEnemiesAsync();
        return list.FirstOrDefault(e => e.Slug == slug)
               ?? await contentService.GetEnemyAsync(slug);
    }

    public async Task<NpcDto?> GetNpcAsync(string slug)
    {
        var list = await GetNpcsAsync();
        return list.FirstOrDefault(n => n.Slug == slug)
               ?? await contentService.GetNpcAsync(slug);
    }

    public async Task<QuestDto?> GetQuestAsync(string slug)
    {
        var list = await GetQuestsAsync();
        return list.FirstOrDefault(q => q.Slug == slug)
               ?? await contentService.GetQuestAsync(slug);
    }

    public async Task<RecipeDto?> GetRecipeAsync(string slug)
    {
        var list = await GetRecipesAsync();
        return list.FirstOrDefault(r => r.Slug == slug)
               ?? await contentService.GetRecipeAsync(slug);
    }

    public async Task<LootTableDto?> GetLootTableAsync(string slug)
    {
        var list = await GetLootTablesAsync();
        return list.FirstOrDefault(t => t.Slug == slug)
               ?? await contentService.GetLootTableAsync(slug);
    }

    public async Task<PowerDto?> GetSpellAsync(string slug)
    {
        var list = await GetSpellsAsync();
        return list.FirstOrDefault(s => s.Slug == slug)
               ?? await contentService.GetSpellAsync(slug);
    }

    public async Task<ActorClassDto?> GetClassAsync(string slug)
    {
        var list = await GetClassesAsync();
        return list.FirstOrDefault(c => c.Slug == slug)
               ?? await contentService.GetClassAsync(slug);
    }

    public async Task<SpeciesDto?> GetOneSpeciesAsync(string slug)
    {
        var list = await GetSpeciesAsync();
        return list.FirstOrDefault(s => s.Slug == slug)
               ?? await contentService.GetSpeciesAsync(slug);
    }

    public async Task<BackgroundDto?> GetBackgroundAsync(string slug)
    {
        var list = await GetBackgroundsAsync();
        return list.FirstOrDefault(b => b.Slug == slug)
               ?? await contentService.GetBackgroundAsync(slug);
    }

    public async Task<SkillDto?> GetSkillAsync(string slug)
    {
        var list = await GetSkillsAsync();
        return list.FirstOrDefault(s => s.Slug == slug)
               ?? await contentService.GetSkillAsync(slug);
    }

    // Cache management
    /// <summary>Clears all cached catalogs so they are re-fetched on next access.</summary>
    public Task InvalidateAsync()
    {
        _abilities  = null;
        _enemies    = null;
        _npcs       = null;
        _quests     = null;
        _recipes    = null;
        _lootTables = null;
        _spells     = null;
        _classes    = null;
        _species    = null;
        _backgrounds = null;
        _skills     = null;
        _zoneLocations = null;
        logger.LogInformation("ContentCache invalidated");
        return Task.CompletedTask;
    }
}
