using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealmForge.Services;

/// <summary>
/// Service for loading, saving, and managing JSON data files
/// </summary>
public class FileManagementService
{
    private readonly ILogger<FileManagementService> _logger;
    private readonly EditorSettingsService _settingsService;

    public FileManagementService(ILogger<FileManagementService> logger, EditorSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Load a JSON file and return as JObject for dynamic editing
    /// </summary>
    public async Task<JObject?> LoadJsonFileAsync(string filePath)
    {
        try
        {
            _logger.LogDebug("Loading JSON file: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return null;
            }

            var jsonText = await File.ReadAllTextAsync(filePath);
            var jsonObject = JObject.Parse(jsonText);
            
            _logger.LogInformation("Successfully loaded: {FileName}", Path.GetFileName(filePath));
            
            // Add to recent files
            await AddToRecentFilesAsync(filePath);
            
            return jsonObject;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in file: {FilePath}", filePath);
            throw new InvalidDataException($"Invalid JSON: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Save a JSON object to file
    /// </summary>
    public async Task SaveJsonFileAsync(string filePath, JObject jsonObject)
    {
        try
        {
            _logger.LogDebug("Saving JSON file: {FilePath}", filePath);
            
            var jsonText = jsonObject.ToString(Formatting.Indented);
            await File.WriteAllTextAsync(filePath, jsonText);
            
            _logger.LogInformation("Successfully saved: {FileName}", Path.GetFileName(filePath));
            
            // Add to recent files
            await AddToRecentFilesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Get all JSON files in a directory recursively
    /// </summary>
    public List<string> GetJsonFiles(string rootPath)
    {
        try
        {
            if (!Directory.Exists(rootPath))
            {
                _logger.LogWarning("Directory not found: {RootPath}", rootPath);
                return new List<string>();
            }

            var files = Directory.GetFiles(rootPath, "*.json", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

            _logger.LogDebug("Found {Count} JSON files in {RootPath}", files.Count, rootPath);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan directory: {RootPath}", rootPath);
            return new List<string>();
        }
    }

    /// <summary>
    /// Add a file to recent files list
    /// </summary>
    private async Task AddToRecentFilesAsync(string filePath)
    {
        await _settingsService.UpdateSettingAsync(settings =>
        {
            // Remove if already exists
            settings.RecentFiles.Remove(filePath);
            
            // Add to front
            settings.RecentFiles.Insert(0, filePath);
            
            // Trim to max size
            if (settings.RecentFiles.Count > settings.MaxRecentFiles)
            {
                settings.RecentFiles = settings.RecentFiles.Take(settings.MaxRecentFiles).ToList();
            }
        });
    }

    /// <summary>
    /// Get recent files from settings
    /// </summary>
    public async Task<List<string>> GetRecentFilesAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        return settings.RecentFiles.Where(File.Exists).ToList();
    }
}
