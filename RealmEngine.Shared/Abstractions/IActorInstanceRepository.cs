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

    /// <summary>
    /// Returns every active instance placed at one zone location, ordered by slug, with its overrides already composed
    /// onto the archetype's template.
    /// </summary>
    /// <param name="locationSlug">Zone-location slug to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The placed instances, or an empty list when nothing stands there.</returns>
    Task<List<PlacedActorInstance>> GetPlacedAsync(string locationSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one instance by slug with its overrides composed onto the archetype's template. This is the reload
    /// path: a spawn row carries its instance slug so the template can be rebuilt with the same overrides.
    /// </summary>
    /// <param name="instanceSlug">Actor instance slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The placed instance, or <see langword="null"/> when the content no longer exists.</returns>
    Task<PlacedActorInstance?> GetTemplateBySlugAsync(string instanceSlug, CancellationToken cancellationToken = default);
}
