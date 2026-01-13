using Blazored.LocalStorage;
using Microsoft.Extensions.Logging;
using RealmForge.Models;

namespace RealmForge.Services;

/// <summary>
/// Service for managing RealmForge editor settings and user preferences
/// </summary>
public class EditorSettingsService
{
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<EditorSettingsService> _logger;
    private const string SettingsKey = "realmforge_settings";
    private EditorSettings? _cachedSettings;

    public EditorSettingsService(ILocalStorageService localStorage, ILogger<EditorSettingsService> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>
    /// Load settings from local storage
    /// </summary>
    public async Task<EditorSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            _cachedSettings = await _localStorage.GetItemAsync<EditorSettings>(SettingsKey);
            
            if (_cachedSettings == null)
            {
                _logger.LogInformation("No saved settings found, creating defaults");
                _cachedSettings = new EditorSettings();
                await SaveSettingsAsync(_cachedSettings);
            }
            else
            {
                _logger.LogDebug("Loaded settings: Theme={Theme}, AutoSaveInterval={Interval}s", 
                    _cachedSettings.Theme, _cachedSettings.AutoSaveIntervalSeconds);
            }

            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _cachedSettings = new EditorSettings();
            return _cachedSettings;
        }
    }

    /// <summary>
    /// Save settings to local storage
    /// </summary>
    public async Task SaveSettingsAsync(EditorSettings settings)
    {
        try
        {
            await _localStorage.SetItemAsync(SettingsKey, settings);
            _cachedSettings = settings;
            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }

    /// <summary>
    /// Update a specific setting and save
    /// </summary>
    public async Task UpdateSettingAsync(Action<EditorSettings> updateAction)
    {
        var settings = await LoadSettingsAsync();
        updateAction(settings);
        await SaveSettingsAsync(settings);
    }

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    public async Task ResetToDefaultsAsync()
    {
        _logger.LogInformation("Resetting settings to defaults");
        _cachedSettings = new EditorSettings();
        await SaveSettingsAsync(_cachedSettings);
    }
}
