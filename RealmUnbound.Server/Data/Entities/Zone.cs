namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// A named zone (area) that players can inhabit concurrently.
/// Static zones are seeded at DB initialisation. Procedural zones are generated on demand.
/// </summary>
public class Zone
{
    public string Id { get; set; } = string.Empty; // e.g. "starting-zone", "town-millhaven"

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ZoneType Type { get; set; } = ZoneType.Tutorial;

    /// <summary>Minimum character level to enter (0 = no restriction).</summary>
    public int MinLevel { get; set; }

    /// <summary>Maximum concurrent players (0 = unlimited).</summary>
    public int MaxPlayers { get; set; }

    /// <summary>True for the zone new characters start in.</summary>
    public bool IsStarter { get; set; }

    // Navigation
    public ICollection<ZoneSession> Sessions { get; set; } = [];
}

public enum ZoneType
{
    Tutorial,
    Town,
    Dungeon,
    Wilderness,
}
