using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Components.Components.Pages;

/// <summary>
/// Represents a single chat message with display metadata for the chat panel.
/// Mirrors the desktop <c>ChatMessageViewModel</c> record but adapted for Blazor binding.
/// </summary>
/// <param name="Channel">The chat channel this message belongs to (Zone, Global, Whisper, System).</param>
/// <param name="Sender">Display name of the character who sent the message.</param>
/// <param name="Message">The message text.</param>
/// <param name="Timestamp">UTC time the message was sent.</param>
/// <param name="IsOwn"><see langword="true"/> when this message was sent by the local character.</param>
public record ChannelMessage(
    string Channel,
    string Sender,
    string Message,
    DateTimeOffset Timestamp,
    bool IsOwn)
{
    /// <summary>Gets the channel CSS class used for color coding.</summary>
    public string ChannelClass => Channel.ToLowerInvariant() switch
    {
        "system" => "game-chat-message-system",
        "whisper" => "game-chat-message-whisper",
        "global" => "game-chat-message-global",
        _ => "game-chat-message-zone"
    };

    /// <summary>Gets the formatted timestamp string for display.</summary>
    public string TimestampFormatted => Timestamp.ToLocalTime().ToString("HH:mm");
}

/// <summary>
/// Code-behind for the <see cref="GameChat"/> component.
/// Manages channel pill state, whisper prefix parsing, and filtered message display.
/// </summary>
public partial class GameChat : INotifyPropertyChanged
{
    private string _inputText = string.Empty;
    private string _activeChannel = "Zone";
    private string? _whisperTarget;
    private IDisposable? _stateSubscription;
    private IDisposable? _chatMessageSubscription;
    private bool _disposed;

    /// <summary>
    /// Gets the list of available channel names for the pill bar.
    /// </summary>
    public static readonly string[] ChannelNames = ["Zone", "Global", "Whisper", "System"];

    /// <summary>
    /// Gets or sets the currently active chat channel for filtering.
    /// </summary>
    public string ActiveChannel
    {
        get => _activeChannel;
        set
        {
            if (_activeChannel != value)
            {
                _activeChannel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FilteredMessages));
            }
        }
    }

    /// <summary>
    /// Gets or sets the target character name for whisper messages.
    /// Set automatically when the input starts with <c>/w</c> or <c>/whisper</c>.
    /// </summary>
    public string? WhisperTarget
    {
        get => _whisperTarget;
        set
        {
            if (_whisperTarget != value)
            {
                _whisperTarget = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWhisperActive));
                OnPropertyChanged(nameof(WhisperPlaceholder));
            }
        }
    }

    /// <summary>Gets whether the whisper channel is currently active (has a target).</summary>
    public bool IsWhisperActive => !string.IsNullOrEmpty(WhisperTarget);

    /// <summary>Gets the placeholder text for the chat input field.</summary>
    public string WhisperPlaceholder => IsWhisperActive
        ? $"Whisper to {WhisperTarget}..."
        : "Type a message or /command...";

    /// <summary>
    /// Gets the list of chat messages filtered by the active channel.
    /// When the active channel is "System", only system messages are shown.
    /// When "Whisper" is active and a target is set, filters to whispers to/from that target.
    /// Otherwise returns messages matching the active channel.
    /// When "Zone" is active, returns all zone and non-whisper/non-system messages.
    /// </summary>
    public IEnumerable<ChannelMessage> FilteredMessages
    {
        get
        {
            var channel = _activeChannel;
            if (string.IsNullOrEmpty(channel))
                return GameState.ChatMessages.Select(MapToChannelMessage);

            return GameState.ChatMessages
                .Where(msg => MatchesChannel(msg, channel))
                .Select(MapToChannelMessage)
                .ToList();
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    [Inject]
    private IGameHubConnectionService Hub { get; set; } = null!;

    [Inject]
    private IGameStateService GameState { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _stateSubscription = GameState.OnStateChanged(() => InvokeAsync(StateHasChanged));

        // Re-render when a chat message is received.
        Action<string> handler = _ => InvokeAsync(StateHasChanged);
        GameState.ChatMessageReceived += handler;
        _chatMessageSubscription = new Subscription(() => GameState.ChatMessageReceived -= handler);
    }

    /// <summary>
    /// Attempts to parse a whisper command from the input and sends the message
    /// to the appropriate hub method.
    /// G82: Messages starting with <c>/</c> that are not whisper commands are sent
    /// as slash commands via <c>SendChatMessage</c>.
    /// </summary>
    private async Task SendMessage()
    {
        var text = _inputText?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            // Whisper parsing: /w username message or /whisper username message
            if (TryParseWhisper(text, out var target, out var whisperMessage))
            {
                await Hub.SendAsync("SendWhisper", new { TargetCharacterName = target, Message = whisperMessage });
            }
            // G82: Slash commands — send via SendChatMessage for server-side parsing.
            else if (text.StartsWith('/'))
            {
                await Hub.SendAsync("SendChatMessage", text);
            }
            else
            {
                // Send as a zone message by default (or global if we want channel context later)
                await Hub.SendAsync("SendZoneMessage", new { Message = text });
            }

            _inputText = string.Empty;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // Message send failed silently — the server will send an error via SystemMessage.
        }
    }

    /// <summary>
    /// Attempts to parse a whisper command from the given input.
    /// Supports <c>/w username message</c> and <c>/whisper username message</c> prefixes.
    /// </summary>
    /// <param name="input">The raw input text.</param>
    /// <param name="target">When this method returns, contains the target character name if parsed successfully.</param>
    /// <param name="message">When this method returns, contains the whisper message body if parsed successfully.</param>
    /// <returns><see langword="true"/> if the input was successfully parsed as a whisper command; otherwise <see langword="false"/>.</returns>
    private static bool TryParseWhisper(string input, out string? target, out string? message)
    {
        target = null;
        message = null;

        const string whisperShort = "/w ";
        const string whisperLong = "/whisper ";

        string? prefix = null;
        if (input.StartsWith(whisperShort, StringComparison.OrdinalIgnoreCase))
            prefix = whisperShort;
        else if (input.StartsWith(whisperLong, StringComparison.OrdinalIgnoreCase))
            prefix = whisperLong;

        if (prefix is null)
            return false;

        var afterPrefix = input[prefix.Length..].TrimStart();
        var spaceIndex = afterPrefix.IndexOf(' ');
        if (spaceIndex > 0)
        {
            target = afterPrefix[..spaceIndex];
            message = afterPrefix[(spaceIndex + 1)..].Trim();
            return !string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(message);
        }

        // Single word after prefix — no message body yet, but we can set the target
        if (!string.IsNullOrEmpty(afterPrefix))
        {
            target = afterPrefix;
            message = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>Handles the Enter key in the chat input field.</summary>
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_inputText))
        {
            await SendMessage();
        }
    }

    /// <summary>Sets the active channel when a channel pill is clicked.</summary>
    /// <param name="channel">The channel name to activate.</param>
    private void SetActiveChannel(string channel)
    {
        ActiveChannel = channel;
    }

    /// <summary>Determines whether a chat message matches the given channel filter.</summary>
    private static bool MatchesChannel(ChatMessage msg, string channel)
    {
        var msgChannel = msg.Channel.ToLowerInvariant();
        var filter = channel.ToLowerInvariant();

        return filter switch
        {
            "zone" => msgChannel is "zone" or "global",
            "global" => msgChannel == "global",
            "whisper" => msgChannel == "whisper",
            "system" => msgChannel == "system",
            _ => true
        };
    }

    /// <summary>Maps a <see cref="ChatMessage"/> from the game state to a <see cref="ChannelMessage"/> for display.</summary>
    private static ChannelMessage MapToChannelMessage(ChatMessage msg)
    {
        var isOwn = msg.Sender == "You" || msg.Sender.StartsWith("To ", StringComparison.Ordinal);
        return new ChannelMessage(msg.Channel, msg.Sender, msg.Message, msg.Timestamp, isOwn);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stateSubscription?.Dispose();
        _chatMessageSubscription?.Dispose();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Simple disposable that invokes an action on dispose.
    /// </summary>
    private sealed class Subscription(Action onDispose) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => onDispose();
    }
}
