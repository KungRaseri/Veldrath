using Microsoft.Extensions.Logging;
using RealmEngine.Data.Services;
using System.Text.RegularExpressions;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for discovering and caching category hierarchies at startup.
/// Eliminates the need for repeated file system traversal during runtime.
/// </summary>
public class CategoryDiscoveryService
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<CategoryDiscoveryService> _logger;
    private Dictionary<string, List<string>> _leafCategoriesByDomain = new();
    private Dictionary<string, CategoryInfo> _categoryInfo = new();
    private bool _initialized = false;

    /// <summary>
    /// Initializes a new instance of the CategoryDiscoveryService class.
    /// </summary>
    public CategoryDiscoveryService(GameDataCache dataCache, ILogger<CategoryDiscoveryService> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers all leaf categories across all domains and caches them.
    /// Should be called once at application startup after GameDataCache initialization.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            _logger.LogWarning("CategoryDiscoveryService already initialized, skipping");
            return;
        }

        _logger.LogInformation("🔍 Discovering all leaf categories...");
        var startTime = DateTime.Now;

        _leafCategoriesByDomain.Clear();
        _categoryInfo.Clear();

        var domains = _dataCache.AllDomains;
        var totalCategories = 0;

        foreach (var domain in domains)
        {
            var leafCategories = DiscoverLeafCategoriesForDomain(domain);
            _leafCategoriesByDomain[domain] = leafCategories;
            totalCategories += leafCategories.Count;

            _logger.LogInformation("  📂 {Domain}: {Count} leaf categories", domain, leafCategories.Count);
        }

        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
        _initialized = true;

        _logger.LogInformation("✅ Category discovery complete: {Total} categories across {Domains} domains ({Time:F1}ms)", 
            totalCategories, domains.Count, elapsed);
    }

    /// <summary>
    /// Gets all leaf categories for a domain (categories with catalog.json files).
    /// Returns cached results after Initialize() is called.
    /// </summary>
    /// <param name="domain">Domain name (e.g., "items", "abilities", "enemies")</param>
    /// <returns>List of leaf category paths (e.g., "weapons/swords", "materials/bone")</returns>
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

    /// <summary>
    /// Finds categories matching a pattern within a domain.
    /// Supports wildcards: "*" (all), "materials/*" (prefix), "weapons/swords" (exact).
    /// </summary>
    /// <param name="domain">Domain name</param>
    /// <param name="pattern">Pattern to match. Examples: "*", "materials/*", "weapons/swords"</param>
    /// <returns>List of matching category paths</returns>
    public IReadOnlyList<string> FindCategories(string domain, string pattern)
    {
        var allCategories = GetLeafCategories(domain);

        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
            return allCategories;

        // Exact match
        if (!pattern.Contains('*'))
        {
            return allCategories.Where(c => c.Equals(pattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Wildcard matching
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        return allCategories.Where(c => regex.IsMatch(c)).ToList();
    }

    /// <summary>
    /// Checks if a specific path is a leaf category (has a catalog.json).
    /// </summary>
    public bool IsLeafCategory(string domain, string path)
    {
        if (!_initialized)
        {
            // Fall back to direct check if not initialized
            return _dataCache.FileExists($"{domain}/{path}/catalog.json");
        }

        var leafCategories = GetLeafCategories(domain);
        return leafCategories.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets information about a specific category.
    /// </summary>
    public CategoryInfo? GetCategoryInfo(string domain, string path)
    {
        var key = $"{domain}/{path}";
        return _categoryInfo.TryGetValue(key, out var info) ? info : null;
    }

    /// <summary>
    /// Discovers all leaf categories for a specific domain.
    /// A leaf category is one that has a catalog.json file.
    /// </summary>
    private List<string> DiscoverLeafCategoriesForDomain(string domain)
    {
        var leafCategories = new List<string>();
        var subdomains = _dataCache.GetSubdomainsForDomain(domain);

        foreach (var subdomain in subdomains)
        {
            DiscoverLeafCategoriesRecursive(domain, subdomain, leafCategories);
        }

        return leafCategories.Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Recursively discovers leaf categories starting from a subdomain.
    /// </summary>
    private void DiscoverLeafCategoriesRecursive(string domain, string currentPath, List<string> results)
    {
        // Check if current path has a catalog (making it a leaf)
        var catalogPath = $"{domain}/{currentPath}/catalog.json";
        if (_dataCache.FileExists(catalogPath))
        {
            results.Add(currentPath);
            
            // Store category info
            var catalogFile = _dataCache.GetFile(catalogPath);
            if (catalogFile?.JsonData != null)
            {
                var itemCount = 0;
                var itemsArray = catalogFile.JsonData["items"];
                if (itemsArray != null)
                {
                    itemCount = itemsArray.Count();
                }

                _categoryInfo[$"{domain}/{currentPath}"] = new CategoryInfo
                {
                    Domain = domain,
                    Path = currentPath,
                    ItemCount = itemCount,
                    IsLeaf = true
                };
            }
            
            return; // Don't recurse deeper - this is a leaf
        }

        // No catalog at this level, check for subcategories
        var subcategoryCatalogs = _dataCache.GetCatalogsBySubdomain(domain, currentPath);
        if (subcategoryCatalogs.Any())
        {
            // Extract paths from catalog file locations
            foreach (var catalog in subcategoryCatalogs)
            {
                var path = catalog.RelativePath;
                if (path.StartsWith($"{domain}/", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith("/catalog.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract category path (e.g., "items/materials/bone/catalog.json" -> "materials/bone")
                    var categoryPath = path.Substring(domain.Length + 1, path.Length - domain.Length - 1 - 13);
                    
                    if (!results.Contains(categoryPath))
                    {
                        results.Add(categoryPath);
                        
                        // Store category info
                        if (catalog.JsonData != null)
                        {
                            var itemCount = 0;
                            var itemsArray = catalog.JsonData["items"];
                            if (itemsArray != null)
                            {
                                itemCount = itemsArray.Count();
                            }

                            _categoryInfo[$"{domain}/{categoryPath}"] = new CategoryInfo
                            {
                                Domain = domain,
                                Path = categoryPath,
                                ItemCount = itemCount,
                                IsLeaf = true
                            };
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets statistics about discovered categories.
    /// </summary>
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

/// <summary>
/// Information about a specific category.
/// </summary>
public class CategoryInfo
{
    /// <summary>
    /// The domain this category belongs to (e.g., "items", "abilities").
    /// </summary>
    public string Domain { get; set; } = string.Empty;
    
    /// <summary>
    /// The category path (e.g., "weapons/swords", "materials/bone").
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of items in this category's catalog.
    /// </summary>
    public int ItemCount { get; set; }
    
    /// <summary>
    /// Whether this is a leaf category (has a catalog.json file).
    /// </summary>
    public bool IsLeaf { get; set; }
}

/// <summary>
/// Statistics about discovered categories.
/// </summary>
public class CategoryStatistics
{
    /// <summary>
    /// Total number of domains discovered.
    /// </summary>
    public int TotalDomains { get; set; }
    
    /// <summary>
    /// Total number of leaf categories across all domains.
    /// </summary>
    public int TotalCategories { get; set; }
    
    /// <summary>
    /// Number of categories per domain.
    /// </summary>
    public Dictionary<string, int> CategoriesByDomain { get; set; } = new();
    
    /// <summary>
    /// Whether the discovery service has been initialized.
    /// </summary>
    public bool IsInitialized { get; set; }
}
