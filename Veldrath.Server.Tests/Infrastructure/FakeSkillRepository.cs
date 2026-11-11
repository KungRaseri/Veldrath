using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ISkillRepository"/> for handler tests: a slug lookup over a fixed catalog.
/// </summary>
/// <remarks>
/// <see cref="ThrowOnLookup"/> exists so tests can prove that a catalog failure degrades to an
/// unresolved entry instead of costing the player their character selection.
/// </remarks>
internal sealed class FakeSkillRepository : ISkillRepository
{
    private readonly Dictionary<string, SkillDefinition> _skills;

    /// <summary>Initializes a new instance of <see cref="FakeSkillRepository"/>.</summary>
    /// <param name="skills">The catalog entries this fake serves.</param>
    public FakeSkillRepository(params SkillDefinition[] skills)
    {
        _skills = skills.ToDictionary(skill => skill.SkillId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets or sets a value indicating whether every lookup throws.</summary>
    public bool ThrowOnLookup { get; set; }

    /// <summary>Gets how many slug lookups have been served.</summary>
    public int LookupCount { get; private set; }

    /// <inheritdoc />
    public Task<SkillDefinition?> GetBySlugAsync(string slug)
    {
        LookupCount++;

        if (ThrowOnLookup)
            throw new InvalidOperationException("skill catalog is unavailable");

        return Task.FromResult(_skills.TryGetValue(slug, out var skill) ? skill : null);
    }

    /// <inheritdoc />
    public Task<List<SkillDefinition>> GetAllAsync() => Task.FromResult(_skills.Values.ToList());

    /// <inheritdoc />
    public Task<List<SkillDefinition>> GetByCategoryAsync(string category) =>
        Task.FromResult(_skills.Values
            .Where(skill => string.Equals(skill.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList());
}
