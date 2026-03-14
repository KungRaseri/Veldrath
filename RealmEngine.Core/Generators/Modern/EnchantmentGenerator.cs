using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Enchantment instances from the enchantment catalog in the database.</summary>
public class EnchantmentGenerator(ContentDbContext db, ILogger<EnchantmentGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates an enchantment by slug reference.</summary>
    public async Task<Enchantment?> GenerateEnchantmentAsync(string reference)
    {
        try
        {
            var entity = await db.Enchantments.AsNoTracking()
                .Where(e => e.IsActive && e.Slug == reference)
                .FirstOrDefaultAsync();

            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating enchantment reference={Reference}", reference);
            return null;
        }
    }

    /// <summary>Generates multiple random enchantments using weighted selection.</summary>
    public async Task<List<Enchantment>> GenerateEnchantmentsAsync(int count)
    {
        try
        {
            var all = await db.Enchantments.AsNoTracking()
                .Where(e => e.IsActive)
                .ToListAsync();

            if (all.Count == 0) return [];

            var result = new List<Enchantment>(count);
            for (int i = 0; i < count; i++)
            {
                var entity = SelectWeighted(all);
                if (entity is not null) result.Add(MapToModel(entity));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating {Count} enchantments", count);
            return [];
        }
    }

    private Data.Entities.Enchantment? SelectWeighted(List<Data.Entities.Enchantment> items)
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

    private static Enchantment MapToModel(Data.Entities.Enchantment e) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Slug = e.Slug,
        Name = e.Slug,
        DisplayName = e.DisplayName ?? e.Slug,
        Value = e.Stats.Value ?? 0,
        RarityWeight = e.RarityWeight,
    };
}
