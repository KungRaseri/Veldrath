using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace Veldrath.Discord.Tests.Infrastructure;

/// <summary>Enemy repository stub backed by a fixed catalog list.</summary>
/// <param name="enemies">Enemies the stub returns from <see cref="GetAllAsync"/>.</param>
public sealed class FakeEnemyRepository(params Enemy[] enemies) : IEnemyRepository
{
    /// <inheritdoc />
    public Task<List<Enemy>> GetAllAsync() => Task.FromResult(enemies.ToList());

    /// <inheritdoc />
    public Task<Enemy?> GetBySlugAsync(string slug) =>
        Task.FromResult(enemies.FirstOrDefault(enemy => enemy.Slug == slug));

    /// <inheritdoc />
    public Task<List<Enemy>> GetByFamilyAsync(string family) => Task.FromResult(new List<Enemy>());
}

/// <summary>Power repository stub backed by a fixed catalog list.</summary>
/// <param name="powers">Powers the stub returns from <see cref="GetAllAsync"/>.</param>
public sealed class FakePowerRepository(params Power[] powers) : IPowerRepository
{
    /// <inheritdoc />
    public Task<List<Power>> GetAllAsync() => Task.FromResult(powers.ToList());

    /// <inheritdoc />
    public Task<Power?> GetBySlugAsync(string slug) =>
        Task.FromResult(powers.FirstOrDefault(power => power.Slug == slug));

    /// <inheritdoc />
    public Task<List<Power>> GetByTypeAsync(string powerType) => Task.FromResult(new List<Power>());

    /// <inheritdoc />
    public Task<List<Power>> GetBySchoolAsync(string school) => Task.FromResult(new List<Power>());
}

/// <summary>Character-class repository stub backed by a fixed catalog list.</summary>
/// <param name="classes">Classes the stub serves; base classes are the entries whose <c>IsSubclass</c> is false.</param>
public sealed class FakeCharacterClassRepository(params CharacterClass[] classes) : ICharacterClassRepository
{
    /// <summary>Gets the number of times <see cref="GetByName"/> was called.</summary>
    public int GetByNameCalls { get; private set; }

    /// <inheritdoc />
    public List<CharacterClass> GetAll() => classes.ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetClassesByType(string classType) =>
        classes.Where(c => c.Name.Equals(classType, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetBaseClasses() => classes.Where(c => !c.IsSubclass).ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetSubclasses() => classes.Where(c => c.IsSubclass).ToList();

    /// <inheritdoc />
    public List<CharacterClass> GetSubclassesForParent(string parentClassId) =>
        classes.Where(c => c.ParentClassId == parentClassId).ToList();

    /// <inheritdoc />
    public CharacterClass? GetById(string id) => classes.FirstOrDefault(c => c.Id == id);

    /// <inheritdoc />
    public CharacterClass? GetByName(string name)
    {
        GetByNameCalls++;
        return classes.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
