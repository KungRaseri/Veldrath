using RealmEngine.Shared.Abstractions;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Deterministic <see cref="IRandomSource"/> for tests.
/// </summary>
/// <remarks>
/// Scripted mode returns the enqueued values in call order and throws when the production code
/// rolls more times than the test scripted, so a test can never silently fall back to real
/// randomness. <see cref="Seeded"/> wraps a fixed-seed <see cref="Random"/> when the test only
/// needs the same decisions on every run.
/// </remarks>
public sealed class FakeRandomSource : IRandomSource
{
    private readonly Queue<int> _ints = new();
    private readonly Queue<double> _doubles = new();
    private readonly Random? _seeded;

    /// <summary>Initializes an empty (scripted) fake; every call must be pre-scripted.</summary>
    public FakeRandomSource()
    {
    }

    private FakeRandomSource(Random seeded) => _seeded = seeded;

    /// <summary>Initializes a scripted fake with the given integer results.</summary>
    /// <param name="scriptedValues">The values returned by successive integer calls, in order.</param>
    public FakeRandomSource(params int[] scriptedValues) => Enqueue(scriptedValues);

    /// <summary>Creates a fake that returns the supplied integer values in order.</summary>
    /// <param name="values">The values returned by successive integer calls, in order.</param>
    /// <returns>A scripted <see cref="FakeRandomSource"/>.</returns>
    public static FakeRandomSource Returning(params int[] values) => new(values);

    /// <summary>Creates a fake that returns the supplied doubles from <see cref="NextDouble"/> in order.</summary>
    /// <param name="values">The values returned by successive <see cref="NextDouble"/> calls, in order.</param>
    /// <returns>A scripted <see cref="FakeRandomSource"/>.</returns>
    public static FakeRandomSource ReturningDoubles(params double[] values) => new FakeRandomSource().EnqueueDoubles(values);

    /// <summary>Creates a deterministic source backed by a seeded <see cref="Random"/>.</summary>
    /// <param name="seed">The seed. The same seed always produces the same sequence.</param>
    /// <returns>A seeded <see cref="IRandomSource"/>.</returns>
    public static IRandomSource Seeded(int seed) => new FakeRandomSource(new Random(seed));

    /// <summary>Gets the number of integer draws made so far.</summary>
    public int IntCallCount { get; private set; }

    /// <summary>Gets the number of <see cref="NextDouble"/> draws made so far.</summary>
    public int DoubleCallCount { get; private set; }

    /// <summary>Gets the number of values still scripted for integer draws.</summary>
    public int RemainingInts => _ints.Count;

    /// <summary>Gets the number of values still scripted for <see cref="NextDouble"/>.</summary>
    public int RemainingDoubles => _doubles.Count;

    /// <summary>Appends integer values to the script.</summary>
    /// <param name="values">The values returned by subsequent integer calls, in order.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeRandomSource Enqueue(params int[] values)
    {
        foreach (var value in values)
        {
            _ints.Enqueue(value);
        }
        return this;
    }

    /// <summary>Appends <see cref="NextDouble"/> values to the script.</summary>
    /// <param name="values">The values returned by subsequent double calls, in order.</param>
    /// <returns>This instance, for chaining.</returns>
    public FakeRandomSource EnqueueDoubles(params double[] values)
    {
        foreach (var value in values)
        {
            _doubles.Enqueue(value);
        }
        return this;
    }

    /// <inheritdoc />
    public int Next(int maxValue)
    {
        IntCallCount++;

        if (_seeded is not null)
        {
            return _seeded.Next(maxValue);
        }

        var value = DequeueInt($"Next({maxValue})");
        if (value < 0 || value >= maxValue)
        {
            throw new InvalidOperationException(
                $"FakeRandomSource scripted {value} for Next({maxValue}), which is outside [0, {maxValue}).");
        }
        return value;
    }

    /// <inheritdoc />
    public int Next(int minValue, int maxValue)
    {
        IntCallCount++;

        if (_seeded is not null)
        {
            return _seeded.Next(minValue, maxValue);
        }

        var value = DequeueInt($"Next({minValue}, {maxValue})");
        if (value < minValue || value >= maxValue)
        {
            throw new InvalidOperationException(
                $"FakeRandomSource scripted {value} for Next({minValue}, {maxValue}), which is outside [{minValue}, {maxValue}).");
        }
        return value;
    }

    /// <inheritdoc />
    public double NextDouble()
    {
        DoubleCallCount++;

        if (_seeded is not null)
        {
            return _seeded.NextDouble();
        }

        if (_doubles.Count == 0)
        {
            throw new InvalidOperationException("FakeRandomSource has no scripted NextDouble() value left.");
        }

        var value = _doubles.Dequeue();
        if (value < 0.0 || value >= 1.0)
        {
            throw new InvalidOperationException(
                $"FakeRandomSource scripted {value} for NextDouble(), which is outside [0.0, 1.0).");
        }
        return value;
    }

    private int DequeueInt(string call)
    {
        if (_ints.Count == 0)
        {
            throw new InvalidOperationException($"FakeRandomSource has no scripted int left for {call}.");
        }
        return _ints.Dequeue();
    }
}
