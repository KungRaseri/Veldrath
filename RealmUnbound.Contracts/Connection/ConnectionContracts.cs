namespace RealmUnbound.Contracts.Connection;

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
