using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Enemy instances from the enemy catalog in the database.</summary>
public class EnemyGenerator(IEnemyRepository repository, ILogger<EnemyGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of random enemies from a specific family/category.</summary>
    public async Task<List<Enemy>> GenerateEnemiesAsync(string category, int count = 5, bool hydrate = true)
    {
        try
        {
            var all = await repository.GetByFamilyAsync(category);
            if (all.Count == 0) return [];
            var result = new List<Enemy>(count);
            for (int i = 0; i < count; i++)
            {
                var item = SelectWeighted(all);
                if (item is not null) result.Add(item);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating enemies category={Category}", category);
            return [];
        }
    }

    /// <summary>Generates a specific enemy by slug.</summary>
    public async Task<Enemy?> GenerateEnemyByNameAsync(string category, string enemyName, bool hydrate = true)
    {
        try
        {
            return await repository.GetBySlugAsync(enemyName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating enemy by name {Name}", enemyName);
            return null;
        }
    }

    private Enemy? SelectWeighted(List<Enemy> items)
    {
        if (items.Count == 0) return null;
        var total = items.Sum(i => i.RarityWeight > 0 ? i.RarityWeight : 1);
        var roll = _random.Next(total);
        var cumulative = 0;
        foreach (var item in items)
        {
            cumulative += item.RarityWeight > 0 ? item.RarityWeight : 1;
            if (roll < cumulative) return item;
        }
        return items[^1];
    }
}
