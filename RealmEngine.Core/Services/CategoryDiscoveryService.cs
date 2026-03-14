using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using System.Text.RegularExpressions;

namespace RealmEngine.Core.Services;

public class CategoryDiscoveryService
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly ILogger<CategoryDiscoveryService> _logger;
    private Dictionary<string, List<string>> _leafCategoriesByDomain = new();
    private Dictionary<string, CategoryInfo> _categoryInfo = new();
    private bool _initialized = false;

    public CategoryDiscoveryService(IDbContextFactory<ContentDbContext> dbFactory, ILogger<CategoryDiscoveryService> logger)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Initialize()
    {
        if (_initialized)
        {
            _logger.LogWarning("CategoryDiscoveryService already initialized, skipping");
            return;
        }

        _logger.LogInformation("Discovering all leaf categories...");
        var startTime = DateTime.Now;

        _leafCategoriesByDomain.Clear();
        _categoryInfo.Clear();

        using var db = _dbFactory.CreateDbContext();

        DiscoverDomain(db, "items", () => db.Items.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey)
            .Union(db.Weapons.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey))
            .Union(db.Armors.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey))
            .Union(db.Materials.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey)));

        DiscoverDomain(db, "abilities", () => db.Abilities.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "enemies", () => db.Enemies.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "npcs", () => db.Npcs.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "quests", () => db.Quests.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "spells", () => db.Spells.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "skills", () => db.Skills.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "recipes", () => db.Recipes.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "enchantments", () => db.Enchantments.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "classes", () => db.CharacterClasses.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "locations", () => db.WorldLocations.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "organizations", () => db.Organizations.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "dialogues", () => db.Dialogues.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));

        var totalCategories = _leafCategoriesByDomain.Values.Sum(v => v.Count);
        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
        _initialized = true;

        _logger.LogInformation("Category discovery complete: {Total} categories across {Domains} domains ({Time:F1}ms)",
            totalCategories, _leafCategoriesByDomain.Count, elapsed);
    }

    private void DiscoverDomain(ContentDbContext db, string domain, Func<IQueryable<string>> queryFactory)
    {
        try
        {
            var typeKeys = queryFactory().Distinct().OrderBy(t => t).ToList();
            _leafCategoriesByDomain[domain] = typeKeys;

            foreach (var typeKey in typeKeys)
            {
                _categoryInfo[$"{domain}/{typeKey}"] = new CategoryInfo
                {
                    Domain = domain,
                    Path = typeKey,
                    ItemCount = queryFactory().Count(t => t == typeKey),
                    IsLeaf = true
                };
            }

            _logger.LogDebug("  {Domain}: {Count} leaf categories", domain, typeKeys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover categories for domain '{Domain}'", domain);
            _leafCategoriesByDomain[domain] = [];
        }
    }

    public IReadOnlyList<string> GetLeafCategories(string domain)
    {
        if (!_initialized)
        {
            _logger.LogWarning("CategoryDiscoveryService not initialized, discovering categories on-demand");
            Initialize();
        }

        return _leafCategoriesByDomain.TryGetValue(domain, out var categories)
            ? categories.AsReadOnly()
            : new List<string>().AsReadOnly();
    }

    public IReadOnlyList<string> FindCategories(string domain, string pattern)
    {
        var allCategories = GetLeafCategories(domain);

        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
            return allCategories;

        if (!pattern.Contains('*'))
            return allCategories.Where(c => c.Equals(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        return allCategories.Where(c => regex.IsMatch(c)).ToList();
    }

    public bool IsLeafCategory(string domain, string path)
    {
        var leafCategories = GetLeafCategories(domain);
        return leafCategories.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    public CategoryInfo? GetCategoryInfo(string domain, string path)
    {
        var key = $"{domain}/{path}";
        return _categoryInfo.TryGetValue(key, out var info) ? info : null;
    }

    public CategoryStatistics GetStatistics()
    {
        return new CategoryStatistics
        {
            TotalDomains = _leafCategoriesByDomain.Count,
            TotalCategories = _leafCategoriesByDomain.Values.Sum(list => list.Count),
            CategoriesByDomain = _leafCategoriesByDomain.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Count
            ),
            IsInitialized = _initialized
        };
    }
}

public class CategoryInfo
{
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public bool IsLeaf { get; set; }
    public List<string> SubCategories { get; set; } = [];
}

public class CategoryStatistics
{
    public int TotalDomains { get; set; }
    public int TotalCategories { get; set; }
    public Dictionary<string, int> CategoriesByDomain { get; set; } = new();
    public bool IsInitialized { get; set; }
}