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
