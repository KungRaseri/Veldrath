namespace RealmEngine.Shared.Models;

/// <summary>
/// Rich information about sockets of a specific type on an item.
/// Provides data for UI display in Godot.
/// </summary>
public class SocketInfo
{
    /// <summary>
    /// Type of sockets this info represents.
    /// </summary>
    public SocketType Type { get; set; }

    /// <summary>
    /// Number of filled sockets.
    /// </summary>
    public int FilledCount { get; set; }

    /// <summary>
    /// Total number of sockets (filled + empty).
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// The actual socket instances.
    /// </summary>
    public List<Socket> Sockets { get; set; } = new();

    /// <summary>
    /// Display text for this socket type (e.g., "Gem: 1/2").
    /// </summary>
    public string DisplayText => $"{Type}: {FilledCount}/{TotalCount}";

    /// <summary>
    /// Whether this socket type has any empty slots.
    /// </summary>
    public bool HasEmptySlots => FilledCount < TotalCount;

    /// <summary>
    /// Whether all sockets of this type are filled.
    /// </summary>
    public bool IsFullySocketed => FilledCount == TotalCount;

    /// <summary>
    /// Percentage of sockets filled (0.0 to 1.0).
    /// </summary>
    public float FillPercentage => TotalCount > 0 ? (float)FilledCount / TotalCount : 0f;
}
