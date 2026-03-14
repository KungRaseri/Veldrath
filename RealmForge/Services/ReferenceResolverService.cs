using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmForge.Models;
using System.Text.RegularExpressions;

namespace RealmForge.Services;

/// <summary>
/// Service for resolving JSON v5.1 references (@domain/path:item syntax)
/// Phase 5: HIGH PRIORITY implementation
/// </summary>
public class ReferenceResolverService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReferenceResolverService> _logger;
    private Dictionary<string, List<ReferenceInfo>> _referenceCache = new();
    private List<ReferenceCategory> _categories = new();
    private bool _isInitialized = false;

    // Reference syntax pattern: @domain/path/subpath:item-name
    private static readonly Regex ReferencePattern = new(@"^@(?<domain>[^/]+)/(?<path>[^:]+):(?<item>[^?\[\]]+)", RegexOptions.Compiled);

    public ReferenceResolverService(IServiceScopeFactory scopeFactory, ILogger<ReferenceResolverService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Parse a reference string into its components
    /// </summary>
    /// <param name="reference">Reference string like @items/weapons/swords:iron-longsword</param>
    /// <returns>Parsed components or null if invalid</returns>
    public (string domain, string path, string itemName)? ParseReference(string reference)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("@"))
            {
                return null;
            }

            var match = ReferencePattern.Match(reference);
            if (!match.Success)
            {
                _logger.LogWarning("Invalid reference format: {Reference}", reference);
                return null;
            }

            var domain = match.Groups["domain"].Value;
            var path = match.Groups["path"].Value;
            var itemName = match.Groups["item"].Value;

            _logger.LogDebug("Parsed reference: Domain={Domain}, Path={Path}, Item={Item}", domain, path, itemName);
            return (domain, path, itemName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing reference: {Reference}", reference);
            return null;
        }
    }

    /// <summary>
    /// Populates the reference catalog from the ContentRegistry table.
    /// </summary>
    public async Task BuildReferenceCatalogAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db == null)
            {
                _isInitialized = true;
                return;
            }

            var entries = await db.ContentRegistry.AsNoTracking().ToListAsync();
            foreach (var entry in entries)
            {
                AddToCache(new ReferenceInfo
                {
                    Domain = entry.Domain,
                    Path = entry.TypeKey,
                    Name = entry.Slug,
                    DisplayName = FormatDisplayName(entry.Slug),
                    Category = entry.Domain,
                    FilePath = $"{entry.TableName}/{entry.EntityId}"
                });
            }

            BuildCategorySummary();
            _isInitialized = true;
            _logger.LogInformation("Reference catalog built: {Count} entries", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build reference catalog");
        }
    }

    /// <summary>
    /// Format a name string for display (kebab-case to Title Case)
    /// </summary>
    private string FormatDisplayName(string name)
    {
        return string.Join(" ", name.Split('-')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1)));
    }

    /// <summary>
    /// Add reference info to cache
    /// </summary>
    private void AddToCache(ReferenceInfo refInfo)
    {
        var key = $"{refInfo.Domain}/{refInfo.Path}";
        
        if (!_referenceCache.ContainsKey(key))
        {
            _referenceCache[key] = new List<ReferenceInfo>();
        }

        _referenceCache[key].Add(refInfo);
    }

    /// <summary>
    /// Build category summary for UI
    /// </summary>
    private void BuildCategorySummary()
    {
        _categories = _referenceCache
            .GroupBy(kvp => kvp.Value.FirstOrDefault()?.Domain ?? "unknown")
            .Select(g => new ReferenceCategory
            {
                Id = g.Key,
                DisplayName = FormatDisplayName(g.Key),
                Icon = GetCategoryIcon(g.Key),
                Count = g.Sum(kvp => kvp.Value.Count),
                Subcategories = g.Select(kvp => kvp.Key).ToList()
            })
            .OrderBy(c => c.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Get icon for a category
    /// </summary>
    private string GetCategoryIcon(string category)
    {
        return category.ToLower() switch
        {
            "items" => "inventory",
            "weapons" => "gavel",
            "armor" => "shield",
            "enemies" => "bug_report",
            "npcs" => "people",
            "abilities" => "flash_on",
            "spells" => "auto_fix_high",
            "skills" => "star",
            "quests" => "assignment",
            "classes" => "school",
            "materials" => "layers",
            "recipes" => "restaurant",
            "world" => "public",
            "organizations" => "corporate_fare",
            "social" => "forum",
            _ => "folder"
        };
    }

    /// <summary>
    /// Resolve a reference string to its target
    /// </summary>
    public ReferenceInfo? ResolveReference(string reference)
    {
        try
        {
            var parsed = ParseReference(reference);
            if (parsed == null)
            {
                return null;
            }

            var (domain, path, itemName) = parsed.Value;
            var key = $"{domain}/{path}";

            if (_referenceCache.TryGetValue(key, out var items))
            {
                return items.FirstOrDefault(i => i.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
            }

            _logger.LogWarning("Reference not found: {Reference}", reference);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving reference: {Reference}", reference);
            return null;
        }
    }

    /// <summary>
    /// Search references by name or category
    /// </summary>
    public List<ReferenceInfo> SearchReferences(string query, string? category = null)
    {
        try
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Reference catalog not initialized");
                return new List<ReferenceInfo>();
            }

            var allReferences = _referenceCache.Values.SelectMany(list => list);

            // Filter by category if specified
            if (!string.IsNullOrWhiteSpace(category))
            {
                allReferences = allReferences.Where(r => r.Domain.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by query if specified
            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowerQuery = query.ToLower();
                allReferences = allReferences.Where(r => 
                    r.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
                    r.DisplayName.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
            }

            return allReferences
                .OrderBy(r => r.Domain)
                .ThenBy(r => r.DisplayName)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching references: Query={Query}, Category={Category}", query, category);
            return new List<ReferenceInfo>();
        }
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    public List<ReferenceCategory> GetCategories()
    {
        return _categories;
    }

    /// <summary>
    /// Get all references for a specific domain/path
    /// </summary>
    public List<ReferenceInfo> GetReferencesForPath(string domain, string path)
    {
        try
        {
            var key = $"{domain}/{path}";
            return _referenceCache.TryGetValue(key, out var items) ? items : new List<ReferenceInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting references for path: {Domain}/{Path}", domain, path);
            return new List<ReferenceInfo>();
        }
    }

    /// <summary>
    /// Check if catalog is initialized
    /// </summary>
    public bool IsInitialized => _isInitialized;
}
