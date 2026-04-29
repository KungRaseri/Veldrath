using System.Text.Json;
using ReactiveUI;

namespace Veldrath.Client;

/// <summary>
/// Mutable client-side configuration shared across ViewModels at runtime.
/// Registered as a singleton so changes are visible to all consumers without restart.
/// </summary>
public class ClientSettings : ReactiveObject
{
    private string _serverBaseUrl;
    private string _foundryBaseUrl;
    private int _masterVolume = 100;
    private int _musicVolume  = 80;
    private int _sfxVolume    = 100;
    private bool _muted;
    private bool _fullScreen;
    private readonly HashSet<Guid> _ignoredCharacterIds;

    /// <summary>Path to the persisted ignore list file. Overridable in tests.</summary>
    internal static string IgnoredFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Veldrath", "ignored.json");

    /// <summary>Initializes a new instance of <see cref="ClientSettings"/> with the given server and Foundry URLs.</summary>
    /// <param name="serverBaseUrl">Initial base URL of the game server, read from <c>appsettings.json</c>.</param>
    /// <param name="foundryBaseUrl">Initial base URL of the Foundry web portal, read from <c>appsettings.json</c>.</param>
    public ClientSettings(string serverBaseUrl, string foundryBaseUrl = "")
    {
        _serverBaseUrl  = serverBaseUrl;
        _foundryBaseUrl = foundryBaseUrl;
        _ignoredCharacterIds = LoadIgnored();
    }

    /// <summary>Gets or sets the base URL of the game server (e.g. <c>http://localhost:9000</c>).</summary>
    public string ServerBaseUrl
    {
        get => _serverBaseUrl;
        set => this.RaiseAndSetIfChanged(ref _serverBaseUrl, value?.TrimEnd('/') ?? string.Empty);
    }

    /// <summary>Gets or sets the base URL of the Foundry community portal (e.g. <c>http://localhost:8081</c>).</summary>
    public string FoundryBaseUrl
    {
        get => _foundryBaseUrl;
        set => this.RaiseAndSetIfChanged(ref _foundryBaseUrl, value);
    }

    // Audio
    /// <summary>Gets or sets the master volume (0–100).</summary>
    public int MasterVolume
    {
        get => _masterVolume;
        set => this.RaiseAndSetIfChanged(ref _masterVolume, Math.Clamp(value, 0, 100));
    }

    /// <summary>Gets or sets the background music volume (0–100).</summary>
    public int MusicVolume
    {
        get => _musicVolume;
        set => this.RaiseAndSetIfChanged(ref _musicVolume, Math.Clamp(value, 0, 100));
    }

    /// <summary>Gets or sets the sound effects volume (0–100).</summary>
    public int SfxVolume
    {
        get => _sfxVolume;
        set => this.RaiseAndSetIfChanged(ref _sfxVolume, Math.Clamp(value, 0, 100));
    }

    /// <summary>Gets or sets whether all audio output is muted.</summary>
    public bool Muted
    {
        get => _muted;
        set => this.RaiseAndSetIfChanged(ref _muted, value);
    }

    // Display
    /// <summary>Gets or sets whether the game runs in full-screen mode.</summary>
    public bool FullScreen
    {
        get => _fullScreen;
        set => this.RaiseAndSetIfChanged(ref _fullScreen, value);
    }

    // Ignore list
    /// <summary>Gets the set of character IDs whose chat messages are filtered out on this client.</summary>
    public IReadOnlySet<Guid> IgnoredCharacterIds => _ignoredCharacterIds;

    /// <summary>Adds a character to the local ignore list and persists the change.</summary>
    /// <param name="id">Character identifier to ignore.</param>
    public void AddIgnored(Guid id)
    {
        _ignoredCharacterIds.Add(id);
        PersistIgnored();
    }

    /// <summary>Removes a character from the local ignore list and persists the change.</summary>
    /// <param name="id">Character identifier to unignore.</param>
    public void RemoveIgnored(Guid id)
    {
        _ignoredCharacterIds.Remove(id);
        PersistIgnored();
    }

    private static HashSet<Guid> LoadIgnored()
    {
        try
        {
            if (File.Exists(IgnoredFilePath))
            {
                var json = File.ReadAllText(IgnoredFilePath);
                return JsonSerializer.Deserialize<HashSet<Guid>>(json) ?? [];
            }
        }
        catch { /* ignore corrupted file — start with empty set */ }
        return [];
    }

    private void PersistIgnored()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IgnoredFilePath)!);
            File.WriteAllText(IgnoredFilePath, JsonSerializer.Serialize(_ignoredCharacterIds));
        }
        catch { /* best-effort persist */ }
    }
}
