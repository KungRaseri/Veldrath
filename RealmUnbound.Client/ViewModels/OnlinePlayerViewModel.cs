using System.Reactive;
using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a player who is currently online in the same zone, shown in the Online in Zone list.</summary>
public class OnlinePlayerViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of <see cref="OnlinePlayerViewModel"/>.</summary>
    /// <param name="name">The player's character name.</param>
    /// <param name="onWhisper">Callback invoked when the player clicks the Whisper button; receives the character name.</param>
    public OnlinePlayerViewModel(string name, Action<string> onWhisper)
    {
        Name = name;
        StartWhisperCommand = ReactiveCommand.Create(() => onWhisper(name));
    }

    /// <summary>Gets the character's display name.</summary>
    public string Name { get; }

    /// <summary>
    /// Sets the active chat channel to Whisper and pre-fills the whisper target with this character's name.
    /// </summary>
    public ReactiveCommand<Unit, Unit> StartWhisperCommand { get; }
}
