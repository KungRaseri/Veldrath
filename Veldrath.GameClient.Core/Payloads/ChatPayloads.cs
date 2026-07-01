namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when a chat message is broadcast to the player.
/// Matches <c>ChatMessageHubDto</c> defined in <c>Veldrath.Server.Hubs.GameHub</c>.
/// </summary>
/// <param name="CharacterId">The sending character's unique identifier.</param>
/// <param name="Channel">The chat channel (e.g. "zone", "global", "whisper", "system").</param>
/// <param name="Sender">The display name of the sender.</param>
/// <param name="Message">The message text.</param>
/// <param name="Timestamp">When the message was sent (UTC).</param>
public sealed record ChatMessageHubDto(
    Guid CharacterId,
    string Channel,
    string Sender,
    string Message,
    DateTimeOffset Timestamp);
