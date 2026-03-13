using ReactiveUI;
using RealmUnbound.Contracts.Characters;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// Per-character entry on the character select screen.
/// Wraps <see cref="CharacterDto"/> and adds a reactive <see cref="IsOnline"/> property
/// so the UI can react to the character going online/offline without replacing the whole item.
/// </summary>
public class CharacterEntryViewModel : ViewModelBase
{
    private bool _isOnline;

    public CharacterDto Character { get; }

    /// <summary>True while another client (or this one) has this character actively in use.</summary>
    public bool IsOnline
    {
        get => _isOnline;
        set => this.RaiseAndSetIfChanged(ref _isOnline, value);
    }

    public CharacterEntryViewModel(CharacterDto character)
    {
        Character = character;
        _isOnline = character.IsOnline;
    }
}
