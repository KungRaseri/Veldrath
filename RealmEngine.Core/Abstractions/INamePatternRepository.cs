using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Repository for reading name-generation pattern sets from the data store.
/// </summary>
public interface INamePatternRepository
{
    /// <summary>Returns all <see cref="NamePatternSet"/> records, each including their patterns and components.</summary>
    Task<IEnumerable<NamePatternSet>> GetAllAsync();

    /// <summary>Returns the <see cref="NamePatternSet"/> for the given entity path, or <c>null</c> if not found.</summary>
    Task<NamePatternSet?> GetByEntityPathAsync(string entityPath);
}
