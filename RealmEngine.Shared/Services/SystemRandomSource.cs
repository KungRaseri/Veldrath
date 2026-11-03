using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Shared.Services;

/// <summary>
/// Default <see cref="IRandomSource"/> implementation backed by <see cref="Random.Shared"/>.
/// This is the production binding: it adds no state of its own and is safe for concurrent use.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    /// <summary>
    /// Gets the process-wide shared instance, used wherever a composition root cannot inject one
    /// (for example the static <c>DiceRoller</c> compatibility overloads).
    /// </summary>
    public static SystemRandomSource Shared { get; } = new();

    /// <inheritdoc />
    public int Next(int maxValue) => Random.Shared.Next(maxValue);

    /// <inheritdoc />
    public int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);

    /// <inheritdoc />
    public double NextDouble() => Random.Shared.NextDouble();
}
