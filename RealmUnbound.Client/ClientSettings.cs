using ReactiveUI;

namespace RealmUnbound.Client;

/// <summary>
/// Mutable client-side configuration shared across ViewModels at runtime.
/// Registered as a singleton so changes made in <c>SettingsViewModel</c> are
/// immediately visible to <c>CharacterSelectViewModel</c> on the next connection attempt.
/// </summary>
public class ClientSettings : ReactiveObject
{
    private string _serverBaseUrl;

    /// <summary>Initializes a new instance of <see cref="ClientSettings"/> with the given server URL.</summary>
    /// <param name="serverBaseUrl">Initial base URL of the game server, read from <c>appsettings.json</c>.</param>
    public ClientSettings(string serverBaseUrl)
    {
        _serverBaseUrl = serverBaseUrl;
    }

    /// <summary>Gets or sets the base URL of the game server (e.g. <c>http://localhost:8080</c>).</summary>
    public string ServerBaseUrl
    {
        get => _serverBaseUrl;
        set => this.RaiseAndSetIfChanged(ref _serverBaseUrl, value);
    }
}
