using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Client.Services;

// ── Interface ──────────────────────────────────────────────────────────────────

public interface IContentService
{
    Task<List<AbilityDto>> GetAbilitiesAsync();
    Task<AbilityDto?> GetAbilityAsync(string slug);

    Task<List<EnemyDto>> GetEnemiesAsync();
    Task<EnemyDto?> GetEnemyAsync(string slug);

    Task<List<NpcDto>> GetNpcsAsync();
    Task<NpcDto?> GetNpcAsync(string slug);

    Task<List<QuestDto>> GetQuestsAsync();
    Task<QuestDto?> GetQuestAsync(string slug);

    Task<List<RecipeDto>> GetRecipesAsync();
    Task<RecipeDto?> GetRecipeAsync(string slug);

    Task<List<LootTableDto>> GetLootTablesAsync();
    Task<LootTableDto?> GetLootTableAsync(string slug);

    Task<List<SpellDto>> GetSpellsAsync();
    Task<SpellDto?> GetSpellAsync(string slug);

    Task<List<ActorClassDto>> GetClassesAsync();
    Task<ActorClassDto?> GetClassAsync(string slug);

    Task<List<SpeciesDto>> GetSpeciesAsync();
    Task<SpeciesDto?> GetSpeciesAsync(string slug);

    Task<List<BackgroundDto>> GetBackgroundsAsync();
    Task<BackgroundDto?> GetBackgroundAsync(string slug);

    Task<List<SkillDto>> GetSkillsAsync();
    Task<SkillDto?> GetSkillAsync(string slug);
}

// ── Implementation ─────────────────────────────────────────────────────────────

public class HttpContentService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpContentService> logger) : IContentService
{
    private AuthenticationHeaderValue? BearerHeader => tokens.BearerHeader();

    private async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<T>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch {Path}", path);
            return [];
        }
    }

    private async Task<T?> GetSingleAsync<T>(string path) where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch {Path}", path);
            return null;
        }
    }

    public Task<List<AbilityDto>> GetAbilitiesAsync()           => GetListAsync<AbilityDto>("api/content/abilities");
    public Task<AbilityDto?> GetAbilityAsync(string slug)       => GetSingleAsync<AbilityDto>($"api/content/abilities/{slug}");

    public Task<List<EnemyDto>> GetEnemiesAsync()               => GetListAsync<EnemyDto>("api/content/enemies");
    public Task<EnemyDto?> GetEnemyAsync(string slug)           => GetSingleAsync<EnemyDto>($"api/content/enemies/{slug}");

    public Task<List<NpcDto>> GetNpcsAsync()                    => GetListAsync<NpcDto>("api/content/npcs");
    public Task<NpcDto?> GetNpcAsync(string slug)               => GetSingleAsync<NpcDto>($"api/content/npcs/{slug}");

    public Task<List<QuestDto>> GetQuestsAsync()                => GetListAsync<QuestDto>("api/content/quests");
    public Task<QuestDto?> GetQuestAsync(string slug)           => GetSingleAsync<QuestDto>($"api/content/quests/{slug}");

    public Task<List<RecipeDto>> GetRecipesAsync()              => GetListAsync<RecipeDto>("api/content/recipes");
    public Task<RecipeDto?> GetRecipeAsync(string slug)         => GetSingleAsync<RecipeDto>($"api/content/recipes/{slug}");

    public Task<List<LootTableDto>> GetLootTablesAsync()        => GetListAsync<LootTableDto>("api/content/loot-tables");
    public Task<LootTableDto?> GetLootTableAsync(string slug)   => GetSingleAsync<LootTableDto>($"api/content/loot-tables/{slug}");

    public Task<List<SpellDto>> GetSpellsAsync()                => GetListAsync<SpellDto>("api/content/spells");
    public Task<SpellDto?> GetSpellAsync(string slug)           => GetSingleAsync<SpellDto>($"api/content/spells/{slug}");

    public Task<List<ActorClassDto>> GetClassesAsync()                  => GetListAsync<ActorClassDto>("api/content/classes");
    public Task<ActorClassDto?> GetClassAsync(string slug)              => GetSingleAsync<ActorClassDto>($"api/content/classes/{slug}");

    public Task<List<SpeciesDto>> GetSpeciesAsync()                     => GetListAsync<SpeciesDto>("api/content/species");
    public Task<SpeciesDto?> GetSpeciesAsync(string slug)               => GetSingleAsync<SpeciesDto>($"api/content/species/{slug}");

    public Task<List<BackgroundDto>> GetBackgroundsAsync()              => GetListAsync<BackgroundDto>("api/content/backgrounds");
    public Task<BackgroundDto?> GetBackgroundAsync(string slug)         => GetSingleAsync<BackgroundDto>($"api/content/backgrounds/{slug}");

    public Task<List<SkillDto>> GetSkillsAsync()                        => GetListAsync<SkillDto>("api/content/skills");
    public Task<SkillDto?> GetSkillAsync(string slug)                   => GetSingleAsync<SkillDto>($"api/content/skills/{slug}");
}
