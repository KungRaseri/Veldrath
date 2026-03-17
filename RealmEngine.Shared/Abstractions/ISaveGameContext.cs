namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Tracks the currently active save game for the running session.
/// Call <see cref="Activate"/> when a player loads or starts a save,
/// and the EF-backed services will automatically scope their queries
/// to that save game row.
/// </summary>
public interface ISaveGameContext
{
    /// <summary>Gets the ID of the currently active save game, or an empty string when no save is loaded.</summary>
    string SaveGameId { get; }

    /// <summary>Sets the active save game ID. Call this after loading or creating a save.</summary>
    void Activate(string saveGameId);
}
