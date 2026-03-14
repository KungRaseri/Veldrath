using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Item instances from the item catalog in the database.</summary>
public class ItemGenerator(
    ContentDbContext db,
    EnchantmentGenerator enchantmentGenerator,
    SocketGenerator socketGenerator,
    BudgetItemGenerationService budgetGenerator,
    ILogger<ItemGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of items matching the given category/type.</summary>
    public async Task<List<Item>> GenerateItemsAsync(string category, int count = 10, bool hydrate = true)
    {
        try
        {
            var all = await db.Items.AsNoTracking()
                .Where(i => i.IsActive && i.TypeKey == category)
                .ToListAsync();

            if (all.Count == 0) return [];

            var result = new List<Item>(count);
            for (int i = 0; i < count; i++)
            {
                var entity = SelectWeighted(all);
                if (entity is not null) result.Add(MapToModel(entity));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating items category={Category}", category);
            return [];
        }
    }

    /// <summary>Generates a specific item by slug.</summary>
    public async Task<Item?> GenerateItemByNameAsync(string category, string itemName, bool hydrate = true)
    {
        try
        {
            var entity = await db.Items.AsNoTracking()
                .Where(i => i.IsActive && i.Slug == itemName)
                .FirstOrDefaultAsync();

            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating item by name {Name}", itemName);
            return null;
        }
    }

    /// <summary>Generates an item using the budget-based generation system.</summary>
    public Task<BudgetItemResult> GenerateItemWithBudgetAsync(BudgetItemRequest request)
        => budgetGenerator.GenerateItemAsync(request);

    /// <summary>Generates multiple items using the budget-based generation system.</summary>
    public async Task<List<BudgetItemResult>> GenerateItemsWithBudgetAsync(BudgetItemRequest request, int count = 10)
    {
        var results = new List<BudgetItemResult>(count);
        for (int i = 0; i < count; i++)
            results.Add(await budgetGenerator.GenerateItemAsync(request));
        return results;
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

    private static Item MapToModel(Data.Entities.Item e) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Slug = e.Slug,
        Name = e.DisplayName ?? e.Slug,
        Price = e.Stats.Value ?? 0,
        Weight = e.Stats.Weight ?? 0f,
    };
}
