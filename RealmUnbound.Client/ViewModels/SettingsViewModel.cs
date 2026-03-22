using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// Settings screen view model. Exposes player-configurable options (audio, display) that
/// take effect immediately at runtime.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ClientSettings _settings;
    private readonly IAudioPlayer _audio;
    private readonly SettingsPersistenceService _persistence;

    /// <summary>Initializes a new instance of <see cref="SettingsViewModel"/>.</summary>
    /// <param name="navigation">Navigation service used to return to the previous screen.</param>
    /// <param name="settings">Shared client settings whose values are exposed for editing.</param>
    /// <param name="audio">Audio player that receives volume/mute changes immediately.</param>
    /// <param name="persistence">Service that saves settings to disk when the user leaves this screen.</param>
    public SettingsViewModel(INavigationService navigation, ClientSettings settings, IAudioPlayer audio,
        SettingsPersistenceService persistence)
    {
        _navigation  = navigation;
        _settings    = settings;
        _audio       = audio;
        _persistence = persistence;

        BackCommand = ReactiveCommand.Create(() =>
        {
            _persistence.Save(_settings);
            _navigation.NavigateTo<MainMenuViewModel>();
        });
    }

    /// <summary>Navigates back to the main menu and persists the current settings to disk.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BackCommand { get; }

    // ── Audio ──────────────────────────────────────────────────────────────────

    /// <summary>Gets or sets the master volume (0–100).</summary>
    public int MasterVolume
    {
        get => _settings.MasterVolume;
        set
        {
            _settings.MasterVolume = value;
            // Master volume scales both channels by adjusting both players.
            _audio.SetMusicVolume((int)(value / 100.0 * _settings.MusicVolume));
            _audio.SetSfxVolume((int)(value / 100.0 * _settings.SfxVolume));
        }
    }

    /// <summary>Gets or sets the background music volume (0–100).</summary>
    public int MusicVolume
    {
        get => _settings.MusicVolume;
        set
        {
            _settings.MusicVolume = value;
            _audio.SetMusicVolume((int)(_settings.MasterVolume / 100.0 * value));
        }
    }

    /// <summary>Gets or sets the sound effects volume (0–100).</summary>
    public int SfxVolume
    {
        get => _settings.SfxVolume;
        set
        {
            _settings.SfxVolume = value;
            _audio.SetSfxVolume((int)(_settings.MasterVolume / 100.0 * value));
        }
    }

    /// <summary>Gets or sets whether all audio is muted.</summary>
    public bool Muted
    {
        get => _settings.Muted;
        set
        {
            _settings.Muted = value;
            _audio.SetMuted(value);
        }
    }

    // ── Display ────────────────────────────────────────────────────────────────

    /// <summary>Gets or sets whether the game runs in full-screen mode.</summary>
    public bool FullScreen
    {
        get => _settings.FullScreen;
        set => _settings.FullScreen = value;
    }
}

