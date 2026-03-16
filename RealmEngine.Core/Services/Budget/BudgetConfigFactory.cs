using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Loads budget-system configuration objects from the database via <see cref="GameConfigService"/>.
/// Falls back to type-level property defaults when a key is absent (InMemory / test mode).
/// </summary>
public class BudgetConfigFactory(GameConfigService configService, ILogger<BudgetConfigFactory> logger)
{
    public BudgetConfig        GetBudgetConfig()    => Load<BudgetConfig>("budget-config");
    public MaterialPools       GetMaterialPools()   => Load<MaterialPools>("material-pools");
    public EnemyTypes          GetEnemyTypes()      => Load<EnemyTypes>("enemy-types");
    public MaterialFilterConfig GetMaterialFilters() => Load<MaterialFilterConfig>("material-filters");
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
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize config key '{Key}' — using type defaults", key);
            return new T();
        }
    }
}

