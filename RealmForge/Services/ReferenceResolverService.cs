using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmForge.Models;
using System.Text.RegularExpressions;

namespace RealmForge.Services;

/// <summary>
/// Service for resolving JSON v5.1 references (@domain/path:item syntax)
/// Phase 5: HIGH PRIORITY implementation
/// </summary>
public class ReferenceResolverService
{
    private readonly ILogger<ReferenceResolverService> _logger;
    private readonly FileManagementService _fileService;
    private Dictionary<string, List<ReferenceInfo>> _referenceCache = new();
    private List<ReferenceCategory> _categories = new();
    private bool _isInitialized = false;

    // Reference syntax pattern: @domain/path/subpath:item-name
    private static readonly Regex ReferencePattern = new(@"^@(?<domain>[^/]+)/(?<path>[^:]+):(?<item>[^?\[\]]+)", RegexOptions.Compiled);

    public ReferenceResolverService(ILogger<ReferenceResolverService> logger, FileManagementService fileService)
    {
        _logger = logger;
        _fileService = fileService;
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
    /// Build reference catalog by scanning all JSON files
    /// </summary>
    public async Task BuildReferenceCatalogAsync(string dataPath)
    {
        try
        {
            _logger.LogInformation("Building reference catalog from: {DataPath}", dataPath);
            
            _referenceCache.Clear();
            _categories.Clear();

            if (!Directory.Exists(dataPath))
            {
                _logger.LogWarning("Data path does not exist: {DataPath}", dataPath);
                return;
            }

            // Scan each domain folder
            var domainFolders = Directory.GetDirectories(dataPath);
            _logger.LogDebug("Found {Count} domain folders", domainFolders.Length);

            foreach (var domainFolder in domainFolders)
            {
                var domainName = Path.GetFileName(domainFolder);
                await ScanDomainAsync(domainName, domainFolder);
            }

            // Build category summary
            BuildCategorySummary();

            _isInitialized = true;
            _logger.LogInformation("Reference catalog built: {ItemCount} items across {CategoryCount} categories", 
                _referenceCache.Values.Sum(list => list.Count), 
                _categories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build reference catalog");
            throw;
        }
    }

    /// <summary>
    /// Scan a domain folder for catalog files
    /// </summary>
    private async Task ScanDomainAsync(string domain, string domainPath)
    {
        try
        {
            _logger.LogDebug("Scanning domain: {Domain}", domain);

            // Find all catalog.json files recursively
            var catalogFiles = Directory.GetFiles(domainPath, "catalog.json", SearchOption.AllDirectories);
            _logger.LogDebug("Found {Count} catalog files in {Domain}", catalogFiles.Length, domain);

            foreach (var catalogFile in catalogFiles)
            {
                await ProcessCatalogFileAsync(domain, catalogFile, domainPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning domain: {Domain}", domain);
        }
    }

    /// <summary>
    /// Process a single catalog.json file
    /// </summary>
    private async Task ProcessCatalogFileAsync(string domain, string catalogFile, string domainRootPath)
    {
        try
        {
            var json = await _fileService.LoadJsonFileAsync(catalogFile);
            
            // Determine the path relative to domain root
            var catalogDir = Path.GetDirectoryName(catalogFile);
            var relativePath = !string.IsNullOrEmpty(catalogDir)
                ? catalogDir.Replace(domainRootPath, "")
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace("\\", "/")
                : "";

            // Check if this is a catalog with items
            if (json is not null && json["items"] is JArray itemsArray)
            {
                _logger.LogDebug("Processing {Count} items from {Path}", itemsArray.Count, catalogFile);

                foreach (var item in itemsArray.Cast<JObject>())
                {
                    var refInfo = CreateReferenceInfo(domain, relativePath, item, catalogFile);
                    if (refInfo != null)
                    {
                        AddToCache(refInfo);
                    }
                }
            }
            else
            {
                _logger.LogDebug("Catalog file has no items array: {Path}", catalogFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing catalog file: {File}", catalogFile);
        }
    }

    /// <summary>
    /// Create a ReferenceInfo from a JSON item
    /// </summary>
    private ReferenceInfo? CreateReferenceInfo(string domain, string path, JObject item, string filePath)
    {
        try
        {
            var name = item["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var refInfo = new ReferenceInfo
            {
                Domain = domain,
                Path = path,
                Name = name,
                DisplayName = FormatDisplayName(name),
                Category = domain,
                FilePath = filePath,
                RarityWeight = item["rarityWeight"]?.Value<int>() ?? 0
            };

            // Extract metadata
            if (item["description"] != null)
                refInfo.Metadata["description"] = item["description"]!.ToString();
            if (item["level"] != null)
                refInfo.Metadata["level"] = item["level"]!.Value<int>();
            if (item["type"] != null)
                refInfo.Metadata["type"] = item["type"]!.ToString();

            return refInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating reference info for item in {File}", filePath);
            return null;
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
