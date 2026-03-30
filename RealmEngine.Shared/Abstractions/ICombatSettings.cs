namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Defines the combat difficulty modifiers used during a combat encounter.
/// Decoupled from save-game state so the server can inject presets without needing <c>ISaveGameService</c>.
/// </summary>
public interface ICombatSettings
{
    /// <summary>Gets the multiplier applied to all player damage output.</summary>
    double PlayerDamageMultiplier { get; }

    /// <summary>Gets the multiplier applied to all enemy damage output.</summary>
    double EnemyDamageMultiplier { get; }

    /// <summary>Gets the multiplier applied to enemy maximum health at encounter start.</summary>
    double EnemyHealthMultiplier { get; }

    /// <summary>Gets the multiplier applied to gold and XP rewards.</summary>
    double GoldXPMultiplier { get; }

    /// <summary>Gets a value indicating whether death is permanent (no respawn) in this session.</summary>
    bool IsPermadeath { get; }
}
