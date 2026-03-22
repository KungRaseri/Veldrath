using System.Text.Json;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Saves and loads <see cref="ClientSettings"/> to/from a plain JSON file on disk
/// so player preferences (volume, display, server URL) survive app restarts.
/// </summary>
public sealed class SettingsPersistenceService
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RealmUnbound", "settings.json");

    private readonly string _filePath;

    /// <summary>Initializes a new instance of <see cref="SettingsPersistenceService"/> using the default app-data path.</summary>
    public SettingsPersistenceService() : this(DefaultPath) { }

    /// <summary>Initializes a new instance of <see cref="SettingsPersistenceService"/> with an explicit file path.</summary>
    /// <param name="filePath">The path to the settings JSON file.</param>
    internal SettingsPersistenceService(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Saves the current values of <paramref name="settings"/> to disk.</summary>
    /// <param name="settings">The settings instance whose values are snapshotted and persisted.</param>
    public void Save(ClientSettings settings)
    {
        var data = new SettingsData(
            settings.ServerBaseUrl,
            settings.MasterVolume,
            settings.MusicVolume,
            settings.SfxVolume,
            settings.Muted,
            settings.FullScreen);

        var json = JsonSerializer.Serialize(data);
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>Loads previously saved settings from disk.</summary>
    /// <returns>The saved <see cref="SettingsData"/>, or <see langword="null"/> if no file exists or it is corrupt.</returns>
    public SettingsData? Load()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<SettingsData>(json);
        }
        catch
        {
            // File corrupted or unreadable — silently discard.
            return null;
        }
    }

    /// <summary>Deletes the settings file if it exists.</summary>
    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}

/// <summary>Immutable snapshot of <see cref="ClientSettings"/> properties persisted to disk.</summary>
public sealed record SettingsData(
    string ServerBaseUrl,
    int    MasterVolume,
    int    MusicVolume,
    int    SfxVolume,
    bool   Muted,
    bool   FullScreen);
