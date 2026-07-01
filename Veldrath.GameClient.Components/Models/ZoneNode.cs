namespace Veldrath.GameClient.Components.Models;

/// <summary>
/// Simplified zone node for the RCL game map display.
/// Represents a single clickable zone card on the region map.
/// </summary>
/// <param name="Id">The zone's unique identifier (slug).</param>
/// <param name="Name">The zone's display name.</param>
/// <param name="Type">The zone type (e.g. "town", "wilderness", "dungeon").</param>
/// <param name="MinLevel">Minimum recommended level, or <c>1</c> if unknown.</param>
/// <param name="IsCurrent"><see langword="true"/> when this is the zone the character is currently in.</param>
/// <param name="IsDiscovered"><see langword="true"/> when the character has discovered this zone.</param>
public sealed record ZoneNode(
    string Id,
    string Name,
    string Type,
    int MinLevel,
    bool IsCurrent,
    bool IsDiscovered)
{
    /// <summary>Gets the CSS class for the zone type badge.</summary>
    public string TypeClass => Type.ToLowerInvariant() switch
    {
        "town" => "zone-type-town",
        "wilderness" => "zone-type-wilderness",
        "dungeon" => "zone-type-dungeon",
        _ => "zone-type-other"
    };

    /// <summary>Gets the display label; shows <c>"???"</c> for undiscovered zones.</summary>
    public string DisplayLabel => IsDiscovered ? Name : "???";

    /// <summary>Gets whether the zone card should be interactive (clickable).</summary>
    public bool IsClickable => IsDiscovered && !IsCurrent;
}
