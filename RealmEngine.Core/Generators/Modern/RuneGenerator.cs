using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Rune instances from items with ItemType=="rune" in the database.</summary>
public class RuneGenerator(ContentDbContext db, ILogger<RuneGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a single random rune, optionally filtered by category.</summary>
    public async Task<Rune?> GenerateAsync(string? category = null)
    {
        try
        {
            var query = db.Items.AsNoTracking().Where(i => i.IsActive && i.ItemType == "rune");
            if (category is not null) query = query.Where(i => i.TypeKey == category);
            var all = await query.ToListAsync();
            if (all.Count == 0) return null;
            var entity = SelectWeighted(all);
            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating rune category={Category}", category);
            return null;
        }
    }

    /// <summary>Generates multiple runes.</summary>
    public async Task<List<Rune>> GenerateManyAsync(int count, string? category = null)
    {
        var result = new List<Rune>(count);
        for (int i = 0; i < count; i++)
        {
            var rune = await GenerateAsync(category);
            if (rune is not null) result.Add(rune);
        }
        return result;
    }

    private Data.Entities.Item? SelectWeighted(List<Data.Entities.Item> items)
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

    private static Rune MapToModel(Data.Entities.Item e) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Slug = e.Slug,
        Name = e.DisplayName ?? e.Slug,
        Price = e.Stats.Value ?? 0,
        RarityWeight = e.RarityWeight,
    };
}
