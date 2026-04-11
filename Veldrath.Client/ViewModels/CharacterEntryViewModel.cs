using Avalonia.Media.Imaging;
using ReactiveUI;
using Veldrath.Contracts.Characters;

namespace Veldrath.Client.ViewModels;

/// <summary>
/// Per-character entry on the character select screen.
/// Wraps <see cref="CharacterDto"/> and adds a reactive <see cref="IsOnline"/> property
/// so the UI can react to the character going online/offline without replacing the whole item.
/// </summary>
public class CharacterEntryViewModel : ViewModelBase
{
    private bool _isOnline;
    private Bitmap? _classIcon;
    private Bitmap? _hardcoreIcon;

    public CharacterDto Character { get; }

    /// <summary>Class badge icon loaded from the asset store, or <see langword="null"/> when assets are unavailable.</summary>
    public Bitmap? ClassIcon
    {
        get => _classIcon;
        set => this.RaiseAndSetIfChanged(ref _classIcon, value);
    }

    /// <summary>Skull icon shown on the HC badge, loaded from the asset store.</summary>
    public Bitmap? HardcoreIcon
    {
        get => _hardcoreIcon;
        set => this.RaiseAndSetIfChanged(ref _hardcoreIcon, value);
    }

    /// <summary>True while another client (or this one) has this character actively in use.</summary>
    public bool IsOnline
    {
        get => _isOnline;
        set => this.RaiseAndSetIfChanged(ref _isOnline, value);
    }

    /// <summary>True if this character was created in hardcore mode.</summary>
    public bool IsHardcore => Character.IsHardcore;

    public CharacterEntryViewModel(CharacterDto character)
    {
        Character = character;
        _isOnline = character.IsOnline;
    }
}
