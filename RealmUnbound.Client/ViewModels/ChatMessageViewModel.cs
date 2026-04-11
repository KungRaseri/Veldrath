namespace Veldrath.Client.ViewModels;

/// <summary>Represents a single chat message displayed in the chat log.</summary>
/// <param name="Channel">The chat channel this message belongs to.</param>
/// <param name="Sender">Display name of the character who sent the message.</param>
/// <param name="Message">The message text.</param>
/// <param name="Timestamp">UTC time the message was sent.</param>
/// <param name="IsOwn"><see langword="true"/> when this message was sent by the local character.</param>
public record ChatMessageViewModel(
    string Channel,
    string Sender,
    string Message,
    DateTimeOffset Timestamp,
    bool IsOwn)
{
    private static readonly Dictionary<string, string> ChannelColors = new()
    {
        ["Zone"]    = "#94a3b8",
        ["Global"]  = "#60a5fa",
        ["Whisper"] = "#f472b6",
        ["System"]  = "#4ade80",
    };

    /// <summary>Gets the channel prefix label, e.g. <c>[Zone]</c>, displayed before the sender name.</summary>
    public string ChannelLabel => $"[{Channel}]";

    /// <summary>Gets the hex colour string for this channel's label (for use in AXAML brushes).</summary>
    public string ChannelColor => ChannelColors.TryGetValue(Channel, out var c) ? c : "#94a3b8";

    /// <summary>
    /// Gets the fully-formatted display string for this message.
    /// Format: <c>[HH:mm] [Channel] Sender: Message</c>.
    /// </summary>
    public string FormattedMessage =>
        $"[{Timestamp.LocalDateTime:HH:mm}] {ChannelLabel} {Sender}: {Message}";
}
