using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using System.Text.RegularExpressions;

namespace RealmEngine.Core.Services;

/// <summary>Discovers and caches all leaf category TypeKeys across every domain by querying the content database.</summary>
public class CategoryDiscoveryService
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly ILogger<CategoryDiscoveryService> _logger;
    private Dictionary<string, List<string>> _leafCategoriesByDomain = new();
    private Dictionary<string, CategoryInfo> _categoryInfo = new();
    private bool _initialized = false;

    /// <summary>Initializes a new instance of <see cref="CategoryDiscoveryService"/>.</summary>
    /// <param name="dbFactory">Factory used to create database contexts for discovery queries.</param>
    /// <param name="logger">Logger instance.</param>
    public CategoryDiscoveryService(IDbContextFactory<ContentDbContext> dbFactory, ILogger<CategoryDiscoveryService> logger)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Queries the database to discover all active leaf-category TypeKeys across all registered domains. Safe to call multiple times — subsequent calls are no-ops.</summary>
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
            .Union(db.Materials.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey)));

        DiscoverDomain(db, "powers", () => db.Powers.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "species", () => db.Species.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "actors", () => db.ActorArchetypes.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "quests", () => db.Quests.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "skills", () => db.Skills.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "recipes", () => db.Recipes.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "enchantments", () => db.Enchantments.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "classes", () => db.ActorClasses.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
        DiscoverDomain(db, "locations", () => db.ZoneLocations.AsNoTracking().Where(i => i.IsActive).Select(i => i.TypeKey));
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

    /// <summary>Returns all discovered leaf-category TypeKeys for the given domain. Triggers a lazy initialization if not yet initialized.</summary>
    /// <param name="domain">The domain name (e.g. "items", "abilities", "quests").</param>
    /// <returns>Read-only list of TypeKey strings for that domain.</returns>
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

    /// <summary>Finds categories in a domain matching an optional wildcard pattern.</summary>
    /// <param name="domain">The domain to search within.</param>
    /// <param name="pattern">Exact name, wildcard pattern (e.g. "sword*"), or <c>*</c> for all.</param>
    /// <returns>Matching category TypeKeys.</returns>
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

    /// <summary>Determines whether the given path is a known leaf category in the specified domain.</summary>
    /// <param name="domain">The domain to check.</param>
    /// <param name="path">The TypeKey path to test.</param>
    /// <returns><see langword="true"/> if the path is a registered leaf category.</returns>
    public bool IsLeafCategory(string domain, string path)
    {
        var leafCategories = GetLeafCategories(domain);
        return leafCategories.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns detailed metadata for a specific category path within a domain.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="path">The TypeKey path.</param>
    /// <returns>The <see cref="CategoryInfo"/>, or <see langword="null"/> if unknown.</returns>
    public CategoryInfo? GetCategoryInfo(string domain, string path)
    {
        var key = $"{domain}/{path}";
        return _categoryInfo.TryGetValue(key, out var info) ? info : null;
    }

    /// <summary>Returns aggregate statistics about the current discovery state.</summary>
    /// <returns>A <see cref="CategoryStatistics"/> snapshot.</returns>
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

/// <summary>Metadata about a single discovered category within a domain.</summary>
public class CategoryInfo
{
    /// <summary>Gets or sets the domain this category belongs to (e.g. "items").</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Gets or sets the TypeKey path for this category.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of active entities with this TypeKey.</summary>
    public int ItemCount { get; set; }

    /// <summary>Gets or sets whether this is a leaf node (has no children).</summary>
    public bool IsLeaf { get; set; }

    /// <summary>Gets or sets child category paths branching from this category.</summary>
    public List<string> SubCategories { get; set; } = [];
}

/// <summary>Aggregate statistics snapshot from <see cref="CategoryDiscoveryService"/>.</summary>
public class CategoryStatistics
{
    /// <summary>Gets or sets the total number of registered domains.</summary>
    public int TotalDomains { get; set; }

    /// <summary>Gets or sets the total number of discovered leaf categories across all domains.</summary>
    public int TotalCategories { get; set; }

    /// <summary>Gets or sets the per-domain category counts.</summary>
    public Dictionary<string, int> CategoriesByDomain { get; set; } = new();

    /// <summary>Gets or sets whether the discovery service has been initialized.</summary>
    public bool IsInitialized { get; set; }
}