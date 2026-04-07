namespace RealmEngine.Shared.Models;

/// <summary>A tile that transitions the player to another zone when stepped on.</summary>
public class ExitTileDefinition
{
    /// <summary>Column of the exit tile.</summary>
    public int TileX { get; set; }

    /// <summary>Row of the exit tile.</summary>
    public int TileY { get; set; }

    /// <summary>Destination zone identifier.</summary>
    public string ToZoneId { get; set; } = string.Empty;
}

/// <summary>A default spawn position for players entering the zone for the first time.</summary>
public class SpawnPointDefinition
{
    /// <summary>Column of the spawn tile.</summary>
    public int TileX { get; set; }

    /// <summary>Row of the spawn tile.</summary>
    public int TileY { get; set; }
}
