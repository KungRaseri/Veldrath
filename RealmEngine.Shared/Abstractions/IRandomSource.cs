namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Abstraction over a source of pseudo-random values.
/// Game logic depends on this interface instead of <see cref="Random.Shared"/> so a host
/// (or a test) can substitute a deterministic sequence and force every branch behind a roll.
/// </summary>
/// <remarks>
/// The contract mirrors the subset of <see cref="Random"/> the engine actually uses:
/// <see cref="Next(int)"/>, <see cref="Next(int, int)"/> and <see cref="NextDouble()"/>.
/// A production host registers <c>SystemRandomSource</c>; tests register a scripted or seeded fake.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Returns a non-negative random integer that is less than the specified maximum.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound. Must be greater than or equal to zero.</param>
    /// <returns>A non-negative integer below <paramref name="maxValue"/>.</returns>
    int Next(int maxValue);

    /// <summary>
    /// Returns a random integer that is at least the specified minimum and less than the specified maximum.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound.</param>
    /// <param name="maxValue">The exclusive upper bound. Must be greater than or equal to <paramref name="minValue"/>.</param>
    /// <returns>An integer in the range [<paramref name="minValue"/>, <paramref name="maxValue"/>).</returns>
    int Next(int minValue, int maxValue);

    /// <summary>
    /// Returns a random floating-point number that is greater than or equal to 0.0 and less than 1.0.
    /// </summary>
    /// <returns>A double in the range [0.0, 1.0).</returns>
    double NextDouble();
}
