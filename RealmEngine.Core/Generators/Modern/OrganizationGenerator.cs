using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates Organization instances from the organization catalog in the database.</summary>
public class OrganizationGenerator(ContentDbContext db, ILogger<OrganizationGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Generates a list of organizations matching the given type.</summary>
    public async Task<List<Organization>> GenerateOrganizationsAsync(string organizationType, int count = 5, bool hydrate = true)
    {
        try
        {
            var all = await db.Organizations.AsNoTracking()
                .Where(o => o.IsActive && o.OrgType == organizationType)
                .ToListAsync();

            if (all.Count == 0) return [];

            var result = new List<Organization>(count);
            for (int i = 0; i < count; i++)
            {
                var entity = SelectWeighted(all);
                if (entity is not null) result.Add(MapToModel(entity));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating organizations type={Type}", organizationType);
            return [];
        }
    }

    /// <summary>Generates a specific organization by slug.</summary>
    public async Task<Organization?> GenerateOrganizationByNameAsync(string organizationType, string organizationName, bool hydrate = true)
    {
        try
        {
            var entity = await db.Organizations.AsNoTracking()
                .Where(o => o.IsActive && o.Slug == organizationName)
                .FirstOrDefaultAsync();

            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating organization by name {Name}", organizationName);
            return null;
        }
    }

    /// <summary>Generates a shop organization.</summary>
    public async Task<Organization?> GenerateShopAsync(string shopType, int inventorySize = 20, bool hydrate = true)
    {
        try
        {
            var all = await db.Organizations.AsNoTracking()
                .Where(o => o.IsActive && (o.OrgType == "shop" || o.Traits.HasShop == true))
                .ToListAsync();

            if (all.Count == 0) return null;
            var entity = SelectWeighted(all);
            return entity is null ? null : MapToModel(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating shop type={Type}", shopType);
            return null;
        }
    }

    private Data.Entities.Organization? SelectWeighted(List<Data.Entities.Organization> items)
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

    private static Organization MapToModel(Data.Entities.Organization e) => new()
    {
        Id = e.Slug,
        Name = e.DisplayName ?? e.Slug,
        Description = string.Empty,
        Type = e.OrgType,
        Wealth = e.Stats.Wealth ?? 0,
    };
}
