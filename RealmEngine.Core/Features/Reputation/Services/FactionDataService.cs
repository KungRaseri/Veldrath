using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Reputation.Services;

/// <summary>
/// Service for loading faction data from the content database.
/// </summary>
public class FactionDataService
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private List<Faction>? _cachedFactions;

    public FactionDataService(IDbContextFactory<ContentDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<Faction> LoadFactions()
    {
        if (_cachedFactions != null)
            return _cachedFactions;

        try
        {
            using var db = _dbFactory.CreateDbContext();
            var orgs = db.Organizations
                .Where(o => o.IsActive && o.OrgType == "faction")
                .ToList();

            _cachedFactions = orgs.Select(o => new Faction
            {
                Id = o.Slug,
                Name = o.DisplayName ?? o.Slug,
                Type = MapOrgTypeToFactionType(o.TypeKey),
                Description = "",
                HomeLocation = "",
                AllyFactionIds = [],
                EnemyFactionIds = [],
            }).ToList();

            _logger.LogInformation("Loaded {Count} factions from database", _cachedFactions.Count);
            return _cachedFactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load factions from database");
            return [];
        }
    }

    public Faction? GetFactionBySlug(string slug)
    {
        var factions = LoadFactions();
        return factions.FirstOrDefault(f =>
            f.Id.Equals(slug, StringComparison.OrdinalIgnoreCase) ||
            f.Name.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public List<Faction> GetFactionsByType(FactionType type)
    {
        return LoadFactions().Where(f => f.Type == type).ToList();
    }

    private static FactionType MapOrgTypeToFactionType(string typeKey)
    {
        return typeKey.ToLowerInvariant() switch
        {
            "criminal" => FactionType.Criminal,
            "religious" => FactionType.Religious,
            "political" or "kingdom" => FactionType.Kingdom,
            _ => FactionType.Guild,
        };
    }
}
