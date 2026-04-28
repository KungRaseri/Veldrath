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

    /// <summary>Initializes a new instance of <see cref="ClientSettings"/> with the given server and Foundry URLs.</summary>
    /// <param name="serverBaseUrl">Initial base URL of the game server, read from <c>appsettings.json</c>.</param>
    /// <param name="foundryBaseUrl">Initial base URL of the Foundry web portal, read from <c>appsettings.json</c>.</param>
    public ClientSettings(string serverBaseUrl, string foundryBaseUrl = "")
    {
        _serverBaseUrl  = serverBaseUrl;
        _foundryBaseUrl = foundryBaseUrl;
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
}
