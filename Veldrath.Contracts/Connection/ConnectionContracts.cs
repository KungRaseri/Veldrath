namespace Veldrath.Contracts.Connection;

/// <summary>
/// Sent by the server to every caller immediately on connection, identifying the server's
/// current version and the oldest client version it will accept. Clients use this payload
/// to decide whether the session is compatible before any gameplay messages are exchanged.
/// </summary>
/// <param name="ConnectionId">The SignalR connection ID assigned to this session.</param>
/// <param name="ServerVersion">The running server version formatted as <c>Major.Minor</c> (e.g. <c>"0.1"</c>).</param>
/// <param name="MinCompatibleClientVersion">
/// The minimum client version the server will accept, formatted as <c>Major.Minor</c>.
/// Clients with a lower version must prompt the user to update before proceeding.
/// </param>
public record ServerInfoPayload(
    string ConnectionId,
    string ServerVersion,
    string MinCompatibleClientVersion);

/// <summary>
/// Server-to-client push: a broadcast announcement from an administrator.
/// Clients should display this prominently (notification banner, chat overlay, etc.).
/// </summary>
/// <param name="Message">Announcement text.</param>
/// <param name="Severity">
/// Hint for rendering. Expected values: <c>"info"</c>, <c>"warning"</c>, <c>"critical"</c>.
/// </param>
public record AnnouncementPayload(string Message, string Severity);

/// <summary>
/// Server-to-client push: notifies the client that it has been forcibly disconnected.
/// The client should close the hub connection and return to the main menu.
/// </summary>
/// <param name="Reason">Human-readable reason for the kick.</param>
public record KickedPayload(string Reason);

/// <summary>Hub request sent by a client to broadcast a chat message or issue a slash-command.</summary>
/// <param name="Message">Raw message text. Prefixing with <c>/</c> triggers command parsing.</param>
public record ChatMessageHubRequest(string Message);

/// <summary>
/// Server-to-client push: delivers a chat message to all players in the same zone.
/// </summary>
/// <param name="CharacterId">Sender's character identifier.</param>
/// <param name="SenderName">Display name of the sending character.</param>
/// <param name="Message">Chat message text.</param>
/// <param name="Timestamp">UTC time the message was processed by the server.</param>
public record ChatMessagePayload(
    Guid CharacterId,
    string SenderName,
    string Message,
    DateTimeOffset Timestamp);

// ── Teleport / Summon ────────────────────────────────────────────────────────

/// <summary>
/// Server-to-client push: the caller's character has been teleported by a staff member.
/// The client should transition to the specified zone.
/// </summary>
/// <param name="ZoneId">Target zone identifier.</param>
/// <param name="ZoneName">Human-readable zone name shown in the notification.</param>
public record TeleportedPayload(string ZoneId, string ZoneName);

/// <summary>
/// Server-to-client push: a staff member is summoning the caller's character to their location.
/// The client should prompt acceptance or auto-accept per settings, then transition to the zone.
/// </summary>
/// <param name="ByCharacterName">Name of the staff character performing the summon.</param>
/// <param name="ZoneId">Destination zone identifier.</param>
public record SummonedPayload(string ByCharacterName, string ZoneId);

// ── Item / Resource Grants ───────────────────────────────────────────────────

/// <summary>
/// Server-to-client push: a staff member has added an item directly to the caller's inventory.
/// </summary>
/// <param name="ItemSlug">The item reference slug.</param>
/// <param name="Quantity">How many were added.</param>
/// <param name="GivenByUsername">Account username of the staff member who granted the item.</param>
public record ItemReceivedPayload(string ItemSlug, int Quantity, string GivenByUsername);

// ── Social ───────────────────────────────────────────────────────────────────

/// <summary>
/// Server-to-client push: a character in the zone performed a roleplay emote.
/// </summary>
/// <param name="CharacterId">Emoting character's identifier.</param>
/// <param name="CharacterName">Emoting character's display name.</param>
/// <param name="Action">Emote action text (e.g. <c>"waves hello"</c>).</param>
public record EmotePayload(Guid CharacterId, string CharacterName, string Action);

/// <summary>
/// Server-to-client push: a private message directed at the caller's character.
/// </summary>
/// <param name="FromCharacterId">Sender's character identifier.</param>
/// <param name="FromCharacterName">Sender's display name.</param>
/// <param name="Message">Private message text.</param>
public record WhisperPayload(Guid FromCharacterId, string FromCharacterName, string Message);

// ── Moderation Notifications ─────────────────────────────────────────────────

/// <summary>
/// Server-to-client push: the caller's account has received a formal warning.
/// </summary>
/// <param name="Reason">Reason for the warning.</param>
/// <param name="NewWarnCount">Updated total warning count on the account.</param>
public record WarnedPayload(string Reason, int NewWarnCount);

/// <summary>
/// Server-to-client push: the caller's account has been muted.
/// The client should display the reason and suppress the chat input field.
/// </summary>
/// <param name="Reason">Optional reason for the mute.</param>
/// <param name="Until">UTC expiry of the mute, or <c>null</c> if permanent.</param>
public record MutedPayload(string? Reason, DateTimeOffset? Until);
