using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Services;

/// <summary>
/// Mutable in-process save game context.
/// Registered as a singleton so the active save ID survives across service resolutions
/// within a desktop session. Call <see cref="Activate"/> once a player loads or
/// creates a save game.
/// </summary>
public sealed class SaveGameContext : ISaveGameContext
{
    private string _saveGameId = string.Empty;

    /// <inheritdoc />
    public string SaveGameId => _saveGameId;

    /// <inheritdoc />
    public void Activate(string saveGameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveGameId);
        _saveGameId = saveGameId;
    }
}
