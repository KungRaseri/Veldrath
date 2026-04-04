using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Base class for all chat tab view models. Each tab owns its own message collection.</summary>
public abstract class ChatTabViewModel : ViewModelBase
{
    private const int MessageCap = 200;

    /// <summary>Gets the text displayed in the tab header strip.</summary>
    public abstract string TabHeader { get; }

    /// <summary>Gets whether this tab can be individually closed by the user.</summary>
    public abstract bool CanClose { get; }

    /// <summary>Gets the command that closes this tab, or <see langword="null"/> when <see cref="CanClose"/> is <see langword="false"/>.</summary>
    public virtual ReactiveCommand<Unit, Unit>? CloseCommand => null;

    /// <summary>Chat messages belonging to this tab (capped at 200; oldest are dropped first).</summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    /// <summary>Appends a message to <see cref="Messages"/>, evicting the oldest entry when the 200-message cap is reached.</summary>
    /// <param name="message">The message to append.</param>
    public void AddMessage(ChatMessageViewModel message)
    {
        if (Messages.Count >= MessageCap)
            Messages.RemoveAt(0);
        Messages.Add(message);
    }
}

/// <summary>Chat tab for global player-to-player messages visible to all connected players.</summary>
public sealed class GlobalChatTabViewModel : ChatTabViewModel
{
    /// <inheritdoc />
    public override string TabHeader => "Global";

    /// <inheritdoc />
    public override bool CanClose => false;
}

/// <summary>Chat tab for zone-scoped messages visible only to players in the current zone.</summary>
public sealed class ZoneChatTabViewModel : ChatTabViewModel
{
    /// <inheritdoc />
    public override string TabHeader => "Zone";

    /// <inheritdoc />
    public override bool CanClose => false;
}

/// <summary>Chat tab for a private whisper conversation with a specific character.</summary>
public sealed class WhisperChatTabViewModel : ChatTabViewModel
{
    /// <summary>Initializes a new instance of <see cref="WhisperChatTabViewModel"/>.</summary>
    /// <param name="targetName">The name of the character being whispered.</param>
    /// <param name="onClose">Callback invoked when the user clicks the close button; receives this tab instance.</param>
    public WhisperChatTabViewModel(string targetName, Action<WhisperChatTabViewModel> onClose)
    {
        TargetName = targetName;
        CloseCommand = ReactiveCommand.Create(() => onClose(this));
    }

    /// <summary>Gets the character name this whisper conversation is directed to.</summary>
    public string TargetName { get; }

    /// <inheritdoc />
    public override string TabHeader => $"W: {TargetName}";

    /// <inheritdoc />
    public override bool CanClose => true;

    /// <inheritdoc />
    public override ReactiveCommand<Unit, Unit>? CloseCommand { get; }
}
