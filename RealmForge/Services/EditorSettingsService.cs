using Microsoft.Extensions.Logging;
using System.Text.Json;
using RealmForge.Models;

namespace RealmForge.Services;

/// <summary>
/// Manages RealmForge editor settings, persisted to %APPDATA%\RealmForge\settings.json
/// </summary>
public class EditorSettingsService
{
    private readonly ILogger<EditorSettingsService> _logger;
    private readonly string _settingsPath;

    private static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RealmForge", "settings.json");

    private EditorSettings? _cachedSettings;

    public EditorSettingsService(ILogger<EditorSettingsService> logger, string? settingsPath = null)
    {
        _logger = logger;
        _settingsPath = settingsPath ?? DefaultSettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    public async Task<EditorSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _cachedSettings = JsonSerializer.Deserialize<EditorSettings>(json);
                if (_cachedSettings != null)
                {
                    _logger.LogDebug("Loaded settings: Theme={Theme}", _cachedSettings.Theme);
                    return _cachedSettings;
                }
            }

            _logger.LogInformation("No saved settings found, creating defaults");
            _cachedSettings = new EditorSettings();
            await SaveSettingsAsync(_cachedSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _cachedSettings = new EditorSettings();
        }

        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(EditorSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            _cachedSettings = settings;
            _logger.LogDebug("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            _cachedSettings = settings;
        }
    }

    public async Task UpdateSettingAsync(Action<EditorSettings> updateAction)
    {
        var settings = await LoadSettingsAsync();
        updateAction(settings);
        await SaveSettingsAsync(settings);
    }

    public async Task ResetToDefaultsAsync()
    {
        _logger.LogInformation("Resetting settings to defaults");
        _cachedSettings = new EditorSettings();
        await SaveSettingsAsync(_cachedSettings);
    }
}
