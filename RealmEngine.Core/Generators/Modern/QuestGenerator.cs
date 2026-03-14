using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Quest instances from the quest catalog in the database.</summary>
public class QuestGenerator(IQuestRepository repository, ILogger<QuestGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of random quests of a given type.</summary>
    public async Task<List<Quest>> GenerateQuestsAsync(string questType, int count = 3, bool hydrate = true)
    {
        try
        {
            var all = await repository.GetByTypeKeyAsync(questType);
            if (all.Count == 0) return [];
            var result = new List<Quest>(count);
            for (int i = 0; i < count; i++)
            {
                var item = SelectWeighted(all);
                if (item is not null) result.Add(item);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating quests questType={QuestType}", questType);
            return [];
        }
    }

    /// <summary>Generates a specific quest by slug.</summary>
    public async Task<Quest?> GenerateQuestByNameAsync(string questType, string questName, bool hydrate = true)
    {
        try
        {
            return await repository.GetBySlugAsync(questName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating quest by name {Name}", questName);
            return null;
        }
    }

    private Quest? SelectWeighted(List<Quest> items)
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
