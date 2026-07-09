using System.Collections.Concurrent;
using Veldrath.Contracts.Auth;

namespace Veldrath.Web.Services;

/// <summary>
/// Singleton cache that bridges authentication state from the SSR prerender pass
/// to the interactive Blazor circuit. <see cref="PersistentComponentState"/> struggles
/// with complex record types (<c>IReadOnlyList<string></c>, <c>DateTimeOffset</c>,
/// <c>Guid?</c>), so this cache stores the full <see cref="AuthResponse"/> and only
/// persists a simple string key via <c>PersistAsJson</c>.
/// </summary>
/// <remarks>
/// Entries expire after 30 seconds to prevent stale data from being served across
/// unrelated page loads.
/// </remarks>
public sealed class AuthStateCache
{
    private readonly ConcurrentDictionary<string, AuthResponse> _cache = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stores an <see cref="AuthResponse"/> and returns a unique key that can be used
    /// to retrieve it later via <see cref="TakeAsync"/>.
    /// </summary>
    public string Store(AuthResponse response)
    {
        var key = Guid.NewGuid().ToString("N");
        _cache[key] = response;

        // Evict the entry after TTL to avoid unbounded growth.
        _ = Task.Run(async () =>
        {
            await Task.Delay(_ttl);
            _cache.TryRemove(key, out _);
        });

        return key;
    }

    /// <summary>
    /// Retrieves and removes the <see cref="AuthResponse"/> associated with the given key.
    /// Returns <c>null</c> if the key is not found or has expired.
    /// </summary>
    public AuthResponse? Take(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        _cache.TryRemove(key, out var response);
        return response;
    }
}
