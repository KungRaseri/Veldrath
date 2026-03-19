using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Loads budget-system configuration objects from the database via <see cref="GameConfigService"/>.
/// Falls back to type-level property defaults when a key is absent (InMemory / test mode).
/// </summary>
public class BudgetConfigFactory(GameConfigService configService, ILogger<BudgetConfigFactory> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Loads the core budget configuration.</summary>
    /// <returns>Loaded or default <see cref="BudgetConfig"/>.</returns>
    public BudgetConfig        GetBudgetConfig()    => Load<BudgetConfig>("budget-config");

    /// <summary>Loads the material pool definitions.</summary>
    /// <returns>Loaded or default <see cref="MaterialPools"/>.</returns>
    public MaterialPools       GetMaterialPools()   => Load<MaterialPools>("material-pools");

    /// <summary>Loads the enemy type definitions used for loot budget scaling.</summary>
    /// <returns>Loaded or default <see cref="EnemyTypes"/>.</returns>
    public EnemyTypes          GetEnemyTypes()      => Load<EnemyTypes>("enemy-types");

    /// <summary>Loads the material filter configuration.</summary>
    /// <returns>Loaded or default <see cref="MaterialFilterConfig"/>.</returns>
    public MaterialFilterConfig GetMaterialFilters() => Load<MaterialFilterConfig>("material-filters");

    /// <summary>Loads the socket system configuration.</summary>
    /// <returns>Loaded or default <see cref="SocketConfig"/>.</returns>
    public SocketConfig        GetSocketConfig()    => Load<SocketConfig>("socket-config");

    private T Load<T>(string key) where T : new()
    {
        var json = configService.GetData(key);
        if (json is null)
        {
            logger.LogDebug("Config key '{Key}' not found — using type defaults", key);
            return new T();
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize config key '{Key}' — using type defaults", key);
            return new T();
        }
    }
}

