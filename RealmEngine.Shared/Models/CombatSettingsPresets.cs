using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Lightweight <see cref="ICombatSettings"/> preset for standard multiplayer sessions.
/// All multipliers are 1.0 and permadeath is disabled.
/// </summary>
public sealed class NormalCombatSettings : ICombatSettings
{
    /// <inheritdoc />
    public double PlayerDamageMultiplier => 1.0;

    /// <inheritdoc />
    public double EnemyDamageMultiplier => 1.0;

    /// <inheritdoc />
    public double EnemyHealthMultiplier => 1.0;

    /// <inheritdoc />
    public double GoldXPMultiplier => 1.0;

    /// <inheritdoc />
    public bool IsPermadeath => false;
}

/// <summary>
/// Lightweight <see cref="ICombatSettings"/> preset for hardcore multiplayer sessions.
/// All multipliers are 1.0 and permadeath is enabled.
/// </summary>
public sealed class HardcoreCombatSettings : ICombatSettings
{
    /// <inheritdoc />
    public double PlayerDamageMultiplier => 1.0;

    /// <inheritdoc />
    public double EnemyDamageMultiplier => 1.0;

    /// <inheritdoc />
    public double EnemyHealthMultiplier => 1.0;

    /// <inheritdoc />
    public double GoldXPMultiplier => 1.0;

    /// <inheritdoc />
    public bool IsPermadeath => true;
}
