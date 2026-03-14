using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates NPC instances from the NPC catalog in the database.</summary>
public class NpcGenerator(INpcRepository repository, ILogger<NpcGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of random NPCs from a specific category.</summary>
    public async Task<List<NPC>> GenerateNpcsAsync(string category, int count = 5, bool hydrate = true)
    {
        try
        {
            var all = await repository.GetByCategoryAsync(category);
            if (all.Count == 0) return [];
            var result = new List<NPC>(count);
            for (int i = 0; i < count; i++)
            {
                var item = SelectWeighted(all);
                if (item is not null) result.Add(item);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating NPCs category={Category}", category);
            return [];
        }
    }

    /// <summary>Generates a specific NPC by slug.</summary>
    public async Task<NPC?> GenerateNpcByNameAsync(string category, string npcName, bool hydrate = true)
    {
        try
        {
            return await repository.GetBySlugAsync(npcName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating NPC by name {Name}", npcName);
            return null;
        }
    }

    private NPC? SelectWeighted(List<NPC> items)
    {
        if (items.Count == 0) return null;
        return items[_random.Next(items.Count)];
    }
}
