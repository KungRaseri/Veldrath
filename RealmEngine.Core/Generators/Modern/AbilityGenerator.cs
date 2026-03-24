using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Power instances from the power catalog in the database.</summary>
public class PowerGenerator(IPowerRepository repository, ILogger<PowerGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of random powers from a specific category and subcategory.</summary>
    public async Task<List<Power>> GenerateAbilitiesAsync(string category, string subcategory, int count = 5, bool hydrate = true)
    {
        try
        {
            var all = await repository.GetByTypeAsync($"{category}/{subcategory}");
            if (all.Count == 0) return [];
            var result = new List<Power>(count);
            for (int i = 0; i < count; i++)
            {
                var item = SelectWeighted(all);
                if (item is not null) result.Add(item);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating abilities category={Category} subcategory={Subcategory}", category, subcategory);
            return [];
        }
    }

    /// <summary>Generates a specific power by name (slug) from the database.</summary>
    public async Task<Power?> GenerateAbilityByNameAsync(string category, string subcategory, string abilityName, bool hydrate = true)
    {
        try
        {
            return await repository.GetBySlugAsync(abilityName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating ability by name {Name}", abilityName);
            return null;
        }
    }

    private Power? SelectWeighted(List<Power> items)
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
