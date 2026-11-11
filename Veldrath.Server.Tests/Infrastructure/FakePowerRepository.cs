using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IPowerRepository"/> for handler tests: a slug lookup over a fixed catalog.
/// </summary>
/// <remarks>
/// <see cref="ThrowOnLookup"/> exists so tests can prove that a catalog failure degrades to an
/// unresolved entry instead of costing the player their character selection.
/// </remarks>
internal sealed class FakePowerRepository : IPowerRepository
{
    private readonly Dictionary<string, Power> _powers;

    /// <summary>Initializes a new instance of <see cref="FakePowerRepository"/>.</summary>
    /// <param name="powers">The catalog entries this fake serves.</param>
    public FakePowerRepository(params Power[] powers)
    {
        _powers = powers.ToDictionary(power => power.Slug, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets or sets a value indicating whether every lookup throws.</summary>
    public bool ThrowOnLookup { get; set; }

    /// <summary>Gets how many slug lookups have been served.</summary>
    public int LookupCount { get; private set; }

    /// <inheritdoc />
    public Task<Power?> GetBySlugAsync(string slug)
    {
        LookupCount++;

        if (ThrowOnLookup)
            throw new InvalidOperationException("power catalog is unavailable");

        return Task.FromResult(_powers.TryGetValue(slug, out var power) ? power : null);
    }

    /// <inheritdoc />
    public Task<List<Power>> GetAllAsync() => Task.FromResult(_powers.Values.ToList());

    /// <inheritdoc />
    public Task<List<Power>> GetByTypeAsync(string powerType) =>
        Task.FromResult(_powers.Values.Where(power => power.Type.ToString().Equals(powerType, StringComparison.OrdinalIgnoreCase)).ToList());

    /// <inheritdoc />
    public Task<List<Power>> GetBySchoolAsync(string school) =>
        Task.FromResult(_powers.Values.Where(power => string.Equals(power.School, school, StringComparison.OrdinalIgnoreCase)).ToList());
}
