using Newtonsoft.Json.Linq;
using Serilog;
using Microsoft.Extensions.Caching.Memory;

namespace RealmEngine.Data.Services;

/// <summary>
/// Centralized cache for all game JSON data loaded at startup.
/// Provides fast in-memory access without repeated file I/O.
/// Uses IMemoryCache for automatic memory management and supports hot-reload via FileSystemWatcher.
/// </summary>
public class GameDataCache : IDisposable
{
    private readonly string _dataRootPath;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, List<string>> _pathsByType;
    private readonly Dictionary<string, List<string>> _pathsByDomain;
    private readonly Dictionary<string, Dictionary<string, List<string>>> _domainHierarchy; // domain -> subdomain -> files
    private readonly Dictionary<string, List<string>> _subdomainsByDomain; // domain -> list of subdomains
    private FileSystemWatcher? _fileWatcher;
    private bool _hotReloadEnabled;
    private bool _disposed;

    // Performance tracking
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalLoadTime;
    private readonly object _statsLock = new object();

    /// <summary>
    /// Initializes a new instance of GameDataCache with the specified data path and optional memory cache.
    /// </summary>
    /// <param name="dataRootPath">Root path to the game data directory</param>
    /// <param name="memoryCache">Optional IMemoryCache instance (creates new if not provided)</param>
    public GameDataCache(string dataRootPath, IMemoryCache? memoryCache = null)
    {
        _dataRootPath = dataRootPath ?? throw new ArgumentNullException(nameof(dataRootPath));
        _cache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());
        _pathsByType = new Dictionary<string, List<string>>();
        _pathsByDomain = new Dictionary<string, List<string>>();
        _domainHierarchy = new Dictionary<string, Dictionary<string, List<string>>>();
        _subdomainsByDomain = new Dictionary<string, List<string>>();

        Log.Information("GameDataCache initialized with path: {Path}", _dataRootPath);
    }

    /// <summary>
    /// Gets the total number of loaded files
    /// </summary>
    public int TotalFilesLoaded => _pathsByType.Values.Sum(list => list.Count);

    /// <summary>
    /// Enables hot-reload: automatically refreshes cache when files change on disk
    /// </summary>
    public void EnableHotReload()
    {
        if (_hotReloadEnabled)
        {
            Log.Warning("Hot-reload already enabled");
            return;
        }

        _fileWatcher = new FileSystemWatcher(_dataRootPath)
        {
            Filter = "*.json",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Renamed += OnFileRenamed;

        _fileWatcher.EnableRaisingEvents = true;
        _hotReloadEnabled = true;

        Log.Information("🔥 Hot-reload enabled - watching {Path} for changes", _dataRootPath);
    }

    /// <summary>
    /// Disables hot-reload file watching
    /// </summary>
    public void DisableHotReload()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
            _hotReloadEnabled = false;
            Log.Information("Hot-reload disabled");
        }
    }

    /// <summary>
    /// Gets statistics about loaded data
    /// </summary>
    public DataCacheStats GetStats()
    {
        lock (_statsLock)
        {
            var stats = new DataCacheStats
            {
                TotalFiles = TotalFilesLoaded,
                CatalogFiles = _pathsByType.ContainsKey(JsonFileType.GenericCatalog.ToString())
                    ? _pathsByType[JsonFileType.GenericCatalog.ToString()].Count : 0,
                NamesFiles = _pathsByType.ContainsKey(JsonFileType.NamesFile.ToString())
                    ? _pathsByType[JsonFileType.NamesFile.ToString()].Count : 0,
                ConfigFiles = _pathsByType.ContainsKey(JsonFileType.ConfigFile.ToString())
                    ? _pathsByType[JsonFileType.ConfigFile.ToString()].Count : 0,
                ComponentFiles = _pathsByType.ContainsKey(JsonFileType.ComponentData.ToString())
                    ? _pathsByType[JsonFileType.ComponentData.ToString()].Count : 0,
                LootTableFiles = _pathsByType.ContainsKey(JsonFileType.LootTableCatalog.ToString())
                    ? _pathsByType[JsonFileType.LootTableCatalog.ToString()].Count : 0,
                Domains = _pathsByDomain.Keys.ToList(),
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                TotalLoadTimeMs = _totalLoadTime
            };
            return stats;
        }
    }

    /// <summary>
    /// Loads all JSON files from the data directory into cache
    /// </summary>
    public void LoadAllData()
    {
        var startTime = DateTime.Now;
        Log.Information("Starting data load from: {Path}", _dataRootPath);

        if (!Directory.Exists(_dataRootPath))
        {
            Log.Warning("Data directory not found: {Path}", _dataRootPath);
            return;
        }

        // Clear existing indexes (cache entries remain until accessed)
        _pathsByType.Clear();
        _pathsByDomain.Clear();
        _domainHierarchy.Clear();
        _subdomainsByDomain.Clear();

        var allJsonFiles = Directory.GetFiles(_dataRootPath, "*.json", SearchOption.AllDirectories);
        Log.Information("Found {Count} JSON files to load", allJsonFiles.Length);

        int loaded = 0;
        int failed = 0;
        var failedFiles = new List<string>();

        foreach (var filePath in allJsonFiles)
        {
            try
            {
                var relativePath = Path.GetRelativePath(_dataRootPath, filePath);
                LoadFileIntoCache(relativePath);
                loaded++;
            }
            catch (Exception ex)
            {
                var relativePath = Path.GetRelativePath(_dataRootPath, filePath);
                Log.Error(ex, "❌ FAILED to load file: {FilePath}", relativePath);
                failed++;
                failedFiles.Add($"{relativePath}: {ex.Message}");
            }
        }

        var elapsed = DateTime.Now - startTime;
        
        if (failed > 0)
        {
            Log.Error("⚠️ Data load completed with ERRORS: {Loaded} loaded, {Failed} FAILED in {Duration}ms",
                loaded, failed, elapsed.TotalMilliseconds);
            Log.Error("Failed files:");
            foreach (var failedFile in failedFiles)
            {
                Log.Error("  - {FailedFile}", failedFile);
            }
            
            // Throw exception to ensure failures aren't silently ignored
            throw new InvalidOperationException(
                $"Failed to load {failed} JSON files. See logs for details. Failed files: {string.Join(", ", failedFiles)}");
        }
        
        Log.Information("✅ Data load complete: ALL {Loaded} files loaded successfully in {Duration}ms",
            loaded, elapsed.TotalMilliseconds);

        LogStats();
    }

    /// <summary>
    /// Loads a file into cache and updates indexes
    /// </summary>
    private void LoadFileIntoCache(string relativePath)
    {
        // Normalize path to use forward slashes for consistent cache keys
        var normalizedPath = NormalizePath(relativePath);
        var absolutePath = Path.Combine(_dataRootPath, relativePath);

        if (!File.Exists(absolutePath))
        {
            Log.Warning("File not found: {Path}", normalizedPath);
            return;
        }

        try
        {
            var jsonText = File.ReadAllText(absolutePath);
            var jsonData = JObject.Parse(jsonText);
            var fileType = DetectFileType(normalizedPath);
            var domain = ExtractDomain(normalizedPath);
            var subdomain = ExtractSubdomain(normalizedPath);

            var cachedFile = new CachedJsonFile
            {
                AbsolutePath = absolutePath,
                RelativePath = normalizedPath,
                FileType = fileType,
                Domain = domain,
                Subdomain = subdomain,
                JsonData = jsonData,
                LastModified = File.GetLastWriteTime(absolutePath)
            };

            // Store in IMemoryCache with normalized path
            var cacheEntry = _cache.CreateEntry($"json:{normalizedPath}");
            cacheEntry.Value = cachedFile;
            cacheEntry.Priority = CacheItemPriority.NeverRemove; // Keep indefinitely
            cacheEntry.Dispose(); // Commit to cache

            // Update indexes for fast querying
            AddToTypeIndex(fileType.ToString(), normalizedPath);
            AddToDomainIndex(domain, normalizedPath);

            // Skip .cbconfig.json files from hierarchy (they're metadata, not game data)
            if (fileType != JsonFileType.ConfigFile)
            {
                AddToHierarchyIndex(domain, subdomain, normalizedPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse JSON file: {Path}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file exists in the cache (without triggering disk load)
    /// </summary>
    /// <param name="relativePath">Path relative to data root (e.g., "abilities/offensive/catalog.json")</param>
    /// <returns>True if file is in cache, false otherwise</returns>
    public bool FileExists(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        return _cache.TryGetValue($"json:{normalizedPath}", out _);
    }

    /// <summary>
    /// Gets a cached file by its relative path
    /// </summary>
    /// <param name="relativePath">Path relative to data root (e.g., "abilities/offensive/catalog.json")</param>
    /// <returns>Cached file or null if not found</returns>
    public CachedJsonFile? GetFile(string relativePath)
    {
        // Normalize path to match cache key format
        var normalizedPath = NormalizePath(relativePath);
        var startTime = DateTime.Now;

        if (_cache.TryGetValue($"json:{normalizedPath}", out CachedJsonFile? cachedFile))
        {
            // Cache hit!
            lock (_statsLock)
            {
                _cacheHits++;
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                _totalLoadTime += (long)elapsed;
            }
            Log.Debug("⚡ CACHE HIT: {Path} ({Time:F3}ms)", normalizedPath, (DateTime.Now - startTime).TotalMilliseconds);
            return cachedFile;
        }

        // Cache miss - try to load it
        lock (_statsLock)
        {
            _cacheMisses++;
        }

        try
        {
            Log.Warning("💾 CACHE MISS: Loading from disk: {Path}", normalizedPath);
            LoadFileIntoCache(relativePath);
            _cache.TryGetValue($"json:{normalizedPath}", out cachedFile);

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Log.Information("💾 Disk load completed: {Path} ({Time:F1}ms)", normalizedPath, elapsed);

            lock (_statsLock)
            {
                _totalLoadTime += (long)elapsed;
            }

            return cachedFile;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load file on-demand: {Path}", relativePath);
            return null;
        }
    }

    /// <summary>
    /// Gets all files of a specific type
    /// </summary>
    public IEnumerable<CachedJsonFile> GetFilesByType(JsonFileType fileType)
    {
        var typeKey = fileType.ToString();
        if (!_pathsByType.TryGetValue(typeKey, out var paths))
            return Enumerable.Empty<CachedJsonFile>();

        return paths.Select(GetFile).Where(f => f != null)!;
    }

    /// <summary>
    /// Gets all files in a specific domain
    /// </summary>
    /// <param name="domain">Domain name</param>
    /// <param name="excludeConfigFiles">If true, excludes .cbconfig.json files (default: true)</param>
    public IEnumerable<CachedJsonFile> GetFilesByDomain(string domain, bool excludeConfigFiles = true)
    {
        if (!_pathsByDomain.TryGetValue(domain, out var paths))
            return Enumerable.Empty<CachedJsonFile>();

        IEnumerable<CachedJsonFile> files = paths.Select(GetFile).Where(f => f != null)!;

        if (excludeConfigFiles)
            files = files.Where(f => f.FileType != JsonFileType.ConfigFile);

        return files;
    }

    /// <summary>
    /// Gets all catalog files
    /// </summary>
    public IEnumerable<CachedJsonFile> GetAllCatalogs() => GetFilesByType(JsonFileType.GenericCatalog);

    /// <summary>
    /// Gets all names files
    /// </summary>
    public IEnumerable<CachedJsonFile> GetAllNames() => GetFilesByType(JsonFileType.NamesFile);

    /// <summary>
    /// Gets all config files
    /// </summary>
    public IEnumerable<CachedJsonFile> GetAllConfigs() => GetFilesByType(JsonFileType.ConfigFile);

    /// <summary>
    /// Gets all component data files
    /// </summary>
    public IEnumerable<CachedJsonFile> GetAllComponentData() => GetFilesByType(JsonFileType.ComponentData);

    /// <summary>
    /// Gets all loot table catalog files
    /// </summary>
    public IEnumerable<CachedJsonFile> GetAllLootTables() => GetFilesByType(JsonFileType.LootTableCatalog);

    /// <summary>
    /// Gets a specific loot table catalog by subdomain (e.g., "harvesting", "enemies", "chests")
    /// </summary>
    public CachedJsonFile? GetLootTableCatalog(string subdomain)
    {
        return GetFile($"loot-tables/{subdomain}/catalog.json");
    }

    /// <summary>
    /// Gets all available domains (top-level categories like 'abilities', 'npcs', etc.)
    /// </summary>
    public IReadOnlyList<string> AllDomains => _pathsByDomain.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Gets all available domains (top-level categories like 'abilities', 'npcs', etc.)
    /// </summary>
    /// <returns>List of domain names</returns>
    [Obsolete("Use AllDomains property instead")]
    public IReadOnlyList<string> GetAllDomains()
    {
        return AllDomains;
    }

    /// <summary>
    /// Gets all subdomains for a specific domain (e.g., 'active', 'passive' for 'abilities')
    /// </summary>
    /// <param name="domain">Domain name</param>
    /// <returns>List of subdomain names for the domain</returns>
    public IReadOnlyList<string> GetSubdomainsForDomain(string domain)
    {
        return _subdomainsByDomain.TryGetValue(domain, out var subdomains)
            ? subdomains.AsReadOnly()
            : new List<string>().AsReadOnly();
    }

    /// <summary>
    /// Gets all files in a specific domain/subdomain combination
    /// </summary>
    /// <param name="domain">Domain name (e.g., 'abilities')</param>
    /// <param name="subdomain">Subdomain name (e.g., 'active')</param>
    /// <param name="excludeConfigFiles">If true, excludes .cbconfig.json files (default: true)</param>
    /// <returns>Files in the specified domain/subdomain</returns>
    public IEnumerable<CachedJsonFile> GetFilesBySubdomain(string domain, string subdomain, bool excludeConfigFiles = true)
    {
        if (!_domainHierarchy.TryGetValue(domain, out var subdomains) ||
            !subdomains.TryGetValue(subdomain, out var paths))
        {
            return Enumerable.Empty<CachedJsonFile>();
        }

        var files = new List<CachedJsonFile>();
        foreach (var path in paths)
        {
            var file = GetFile(path);
            if (file != null)
            {
                // Skip config files if requested (default behavior)
                if (excludeConfigFiles && file.FileType == JsonFileType.ConfigFile)
                    continue;

                files.Add(file);
            }
        }
        return files;
    }

    /// <summary>
    /// Gets the complete domain hierarchy as a nested dictionary
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> DomainHierarchy
    {
        get
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>();

            foreach (var (domain, subdomains) in _domainHierarchy)
            {
                var subdomainDict = new Dictionary<string, IReadOnlyList<string>>();
                foreach (var (subdomain, files) in subdomains)
                {
                    subdomainDict[subdomain] = files.AsReadOnly();
                }
                result[domain] = subdomainDict;
            }

            return result;
        }
    }

    /// <summary>
    /// Gets the complete domain hierarchy as a nested dictionary
    /// </summary>
    /// <returns>Dictionary of domain -> subdomain -> file paths</returns>
    [Obsolete("Use DomainHierarchy property instead")]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> GetDomainHierarchy()
    {
        return DomainHierarchy;
    }

    /// <summary>
    /// Gets all catalog files in a specific domain (excludes .cbconfig.json automatically)
    /// </summary>
    /// <param name="domain">Domain name (e.g., 'abilities', 'npcs')</param>
    /// <returns>All catalog.json files in the domain</returns>
    public IEnumerable<CachedJsonFile> GetCatalogsByDomain(string domain)
    {
        return GetFilesByDomain(domain, excludeConfigFiles: true)
            .Where(f => f.FileType == JsonFileType.GenericCatalog);
    }

    /// <summary>
    /// Gets all catalog files in a specific domain/subdomain combination (excludes .cbconfig.json automatically)
    /// </summary>
    /// <param name="domain">Domain name (e.g., 'abilities')</param>
    /// <param name="subdomain">Subdomain name (e.g., 'active')</param>
    /// <returns>All catalog.json files in the domain/subdomain</returns>
    public IEnumerable<CachedJsonFile> GetCatalogsBySubdomain(string domain, string subdomain)
    {
        return GetFilesBySubdomain(domain, subdomain, excludeConfigFiles: true)
            .Where(f => f.FileType == JsonFileType.GenericCatalog);
    }

    /// <summary>
    /// Gets all names files in a specific domain (excludes .cbconfig.json automatically)
    /// </summary>
    /// <param name="domain">Domain name (e.g., 'abilities', 'npcs')</param>
    /// <returns>All names.json files in the domain</returns>
    public IEnumerable<CachedJsonFile> GetNamesByDomain(string domain)
    {
        return GetFilesByDomain(domain, excludeConfigFiles: true)
            .Where(f => f.FileType == JsonFileType.NamesFile);
    }

    /// <summary>
    /// Gets all names files in a specific domain/subdomain combination (excludes .cbconfig.json automatically)
    /// </summary>
    /// <param name="domain">Domain name (e.g., 'abilities')</param>
    /// <param name="subdomain">Subdomain name (e.g., 'active')</param>
    /// <returns>All names.json files in the domain/subdomain</returns>
    public IEnumerable<CachedJsonFile> GetNamesBySubdomain(string domain, string subdomain)
    {
        return GetFilesBySubdomain(domain, subdomain, excludeConfigFiles: true)
            .Where(f => f.FileType == JsonFileType.NamesFile);
    }

    /// <summary>
    /// Reloads a specific file from disk
    /// </summary>
    public void ReloadFile(string relativePath)
    {
        var normalizedPath = NormalizePath(relativePath);
        Log.Information("Reloading file: {Path}", normalizedPath);

        // Remove from cache
        _cache.Remove($"json:{normalizedPath}");

        // Remove from indexes
        RemoveFromIndexes(relativePath);

        // Reload
        try
        {
            LoadFileIntoCache(relativePath);
            Log.Information("✅ File reloaded: {Path}", relativePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload file: {Path}", relativePath);
        }
    }

    #region File System Watcher Handlers

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: ignore rapid successive changes
        System.Threading.Thread.Sleep(100);

        var relativePath = Path.GetRelativePath(_dataRootPath, e.FullPath);
        Log.Information("🔥 File changed: {Path}", relativePath);
        ReloadFile(relativePath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var relativePath = Path.GetRelativePath(_dataRootPath, e.FullPath);
        Log.Information("🔥 File deleted: {Path}", relativePath);

        _cache.Remove($"json:{relativePath}");
        RemoveFromIndexes(relativePath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var oldRelativePath = Path.GetRelativePath(_dataRootPath, e.OldFullPath);
        var newRelativePath = Path.GetRelativePath(_dataRootPath, e.FullPath);

        Log.Information("🔥 File renamed: {OldPath} → {NewPath}", oldRelativePath, newRelativePath);

        // Remove old
        _cache.Remove($"json:{oldRelativePath}");
        RemoveFromIndexes(oldRelativePath);

        // Add new
        LoadFileIntoCache(newRelativePath);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Normalizes a path to use forward slashes for consistent cache keys
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private void AddToTypeIndex(string typeKey, string relativePath)
    {
        if (!_pathsByType.ContainsKey(typeKey))
            _pathsByType[typeKey] = new List<string>();

        if (!_pathsByType[typeKey].Contains(relativePath))
            _pathsByType[typeKey].Add(relativePath);
    }

    private void AddToDomainIndex(string domain, string relativePath)
    {
        if (string.IsNullOrEmpty(domain))
            return;

        if (!_pathsByDomain.ContainsKey(domain))
            _pathsByDomain[domain] = new List<string>();

        if (!_pathsByDomain[domain].Contains(relativePath))
            _pathsByDomain[domain].Add(relativePath);
    }

    private void RemoveFromIndexes(string relativePath)
    {
        foreach (var list in _pathsByType.Values)
            list.Remove(relativePath);

        foreach (var list in _pathsByDomain.Values)
            list.Remove(relativePath);
    }

    private JsonFileType DetectFileType(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();

        if (fileName == ".cbconfig.json")
            return JsonFileType.ConfigFile;
        
        // Recognize game configuration files
        if (fileName == "socket_config.json" || fileName == "budget_config.json")
            return JsonFileType.ConfigFile;

        if (fileName == "catalog.json")
        {
            // Loot table catalogs are in loot-tables domain
            var domain = ExtractDomain(relativePath);
            if (domain == "loot-tables")
                return JsonFileType.LootTableCatalog;
            
            return JsonFileType.GenericCatalog;
        }

        if (fileName == "names.json")
            return JsonFileType.NamesFile;

        if (fileName.EndsWith(".json") &&
            (fileName.Contains("colors") || fileName.Contains("traits") ||
             fileName.Contains("objectives") || fileName.Contains("materials") ||
             fileName.Contains("rarity_config") || fileName.Contains("sizes") ||
             fileName.Contains("types")))
        {
            return JsonFileType.ComponentData;
        }

        return JsonFileType.Unknown;
    }

    private string ExtractDomain(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private string ExtractSubdomain(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // If there are only 2 parts (domain/file.json), there's no subdomain
        if (parts.Length <= 2)
            return string.Empty;

        // Return the second path component (the folder between domain and file)
        return parts[1];
    }

    private void AddToHierarchyIndex(string domain, string subdomain, string relativePath)
    {
        // Initialize domain if it doesn't exist
        if (!_domainHierarchy.ContainsKey(domain))
            _domainHierarchy[domain] = new Dictionary<string, List<string>>();

        // Initialize subdomain list for this domain
        if (!_subdomainsByDomain.ContainsKey(domain))
            _subdomainsByDomain[domain] = new List<string>();

        // Only add to subdomain list if it's a real folder (not empty, not a filename)
        // Subdomains should only be directory names, not filenames
        if (!string.IsNullOrEmpty(subdomain) &&
            !subdomain.Contains('.') && // Exclude filenames with extensions
            !_subdomainsByDomain[domain].Contains(subdomain))
        {
            _subdomainsByDomain[domain].Add(subdomain);
        }

        // Use "root" as subdomain for files directly in the domain folder
        var effectiveSubdomain = string.IsNullOrEmpty(subdomain) ? "root" : subdomain;

        // Initialize subdomain file list if it doesn't exist
        if (!_domainHierarchy[domain].ContainsKey(effectiveSubdomain))
            _domainHierarchy[domain][effectiveSubdomain] = new List<string>();

        // Add file to subdomain if not already present
        if (!_domainHierarchy[domain][effectiveSubdomain].Contains(relativePath))
            _domainHierarchy[domain][effectiveSubdomain].Add(relativePath);
    }

    private void LogStats()
    {
        var stats = GetStats();
        Log.Information("Cache Statistics:");
        Log.Information("  Total Files: {Total}", stats.TotalFiles);
        Log.Information("  Catalogs: {Catalogs}", stats.CatalogFiles);
        Log.Information("  Names: {Names}", stats.NamesFiles);
        Log.Information("  Configs: {Configs}", stats.ConfigFiles);
        Log.Information("  Component Data: {Components}", stats.ComponentFiles);
        Log.Information("  Loot Tables: {LootTables}", stats.LootTableFiles);
        Log.Information("  Domains: {Domains}", string.Join(", ", stats.Domains));
    }

    /// <summary>
    /// Logs performance statistics (cache efficiency)
    /// </summary>
    public void LogPerformanceStats()
    {
        var stats = GetStats();
        Log.Information("=== Cache Performance Statistics ===");
        Log.Information("  Total Requests: {Total}", stats.TotalRequests);
        Log.Information("  Cache Hits: {Hits} ⚡", stats.CacheHits);
        Log.Information("  Cache Misses: {Misses} 💾", stats.CacheMisses);
        Log.Information("  Hit Rate: {Rate:F2}%", stats.CacheHitRate);
        Log.Information("  Avg Load Time: {AvgTime}ms", stats.AverageLoadTimeMs);
        Log.Information("  Total Load Time: {TotalTime}ms", stats.TotalLoadTimeMs);
        Log.Information("====================================");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the GameDataCache and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the GameDataCache with optional resource cleanup.
    /// </summary>
    /// <param name="disposing">True to release managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            DisableHotReload();

            if (_cache is IDisposable disposableCache)
                disposableCache.Dispose();
        }

        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Cached JSON file with metadata
/// </summary>
public class CachedJsonFile
{
    /// <summary>
    /// Full filesystem path to the JSON file
    /// </summary>
    public required string AbsolutePath { get; init; }
    
    /// <summary>
    /// Path relative to data root directory
    /// </summary>
    public required string RelativePath { get; init; }
    
    /// <summary>
    /// Type of JSON file (catalog, names, config, etc.)
    /// </summary>
    public required JsonFileType FileType { get; init; }
    
    /// <summary>
    /// Top-level domain category (e.g., 'abilities', 'npcs')
    /// </summary>
    public required string Domain { get; init; }
    
    /// <summary>
    /// Subdomain within the domain (e.g., 'active', 'passive')
    /// </summary>
    public required string Subdomain { get; init; }
    
    /// <summary>
    /// Parsed JSON data as JObject
    /// </summary>
    public required JObject JsonData { get; init; }
    
    /// <summary>
    /// Last modification timestamp of the file
    /// </summary>
    public required DateTime LastModified { get; init; }
}

/// <summary>
/// Statistics about cached data
/// </summary>
public class DataCacheStats
{
    /// <summary>
    /// Total number of files loaded in cache
    /// </summary>
    public int TotalFiles { get; set; }
    
    /// <summary>
    /// Number of catalog files
    /// </summary>
    public int CatalogFiles { get; set; }
    
    /// <summary>
    /// Number of names files
    /// </summary>
    public int NamesFiles { get; set; }
    
    /// <summary>
    /// Number of config files
    /// </summary>
    public int ConfigFiles { get; set; }
    
    /// <summary>
    /// Number of component data files
    /// </summary>
    public int ComponentFiles { get; set; }
    
    /// <summary>
    /// Number of loot table catalog files
    /// </summary>
    public int LootTableFiles { get; set; }
    
    /// <summary>
    /// List of all available domains
    /// </summary>
    public List<string> Domains { get; set; } = new();

    // Performance metrics
    /// <summary>
    /// Number of cache hits (data found in memory)
    /// </summary>
    public long CacheHits { get; set; }
    
    /// <summary>
    /// Number of cache misses (data loaded from disk)
    /// </summary>
    public long CacheMisses { get; set; }
    
    /// <summary>
    /// Total number of cache requests
    /// </summary>
    public long TotalRequests => CacheHits + CacheMisses;
    
    /// <summary>
    /// Cache hit rate percentage
    /// </summary>
    public double CacheHitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;
    
    /// <summary>
    /// Average load time per request in milliseconds
    /// </summary>
    public long AverageLoadTimeMs => TotalRequests > 0 ? TotalLoadTimeMs / TotalRequests : 0;
    
    /// <summary>
    /// Total cumulative load time in milliseconds
    /// </summary>
    public long TotalLoadTimeMs { get; set; }
}

/// <summary>
/// Types of JSON files in the game data
/// </summary>
public enum JsonFileType
{
    /// <summary>
    /// Unknown or unrecognized file type
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Names file for procedural name generation
    /// </summary>
    NamesFile,
    
    /// <summary>
    /// Generic catalog file containing item/enemy/NPC definitions
    /// </summary>
    GenericCatalog,
    
    /// <summary>
    /// Component data file for shared attributes
    /// </summary>
    ComponentData,
    
    /// <summary>
    /// Configuration file (.cbconfig.json) for ContentBuilder metadata
    /// </summary>
    ConfigFile,
    
    /// <summary>
    /// Loot table catalog file (loot-tables/*/catalog.json)
    /// </summary>
    LootTableCatalog
}
