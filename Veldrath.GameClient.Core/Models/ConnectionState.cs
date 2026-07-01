namespace Veldrath.GameClient.Core.Models;

/// <summary>
/// Represents the current state of the SignalR connection to the game hub.
/// Used by <see cref="Abstractions.IGameHubConnectionService"/> to report connection status
/// and by consumers to react to connectivity changes.
/// </summary>
public enum ConnectionState
{
    /// <summary>The connection has not been established or has been intentionally closed.</summary>
    Disconnected,

    /// <summary>The connection is in the process of being established.</summary>
    Connecting,

    /// <summary>The connection is established and healthy.</summary>
    Connected,

    /// <summary>
    /// The connection is established but experiencing degraded performance
    /// (e.g. high latency or packet loss).
    /// </summary>
    Degraded,

    /// <summary>The SignalR client is attempting to automatically reconnect after a transient failure.</summary>
    Reconnecting,

    /// <summary>The connection failed and automatic retry has been exhausted or is not configured.</summary>
    Failed
}
