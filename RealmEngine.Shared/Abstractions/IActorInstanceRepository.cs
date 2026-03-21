using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading actor instance catalog data.</summary>
public interface IActorInstanceRepository
{
    /// <summary>Returns all active actor instances.</summary>
    Task<List<ActorInstanceEntry>> GetAllAsync();

    /// <summary>Returns a single actor instance by slug, or <see langword="null"/> if not found.</summary>
    Task<ActorInstanceEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active actor instances with the given type key (e.g. "boss", "story", "unique").</summary>
    Task<List<ActorInstanceEntry>> GetByTypeKeyAsync(string typeKey);
}
