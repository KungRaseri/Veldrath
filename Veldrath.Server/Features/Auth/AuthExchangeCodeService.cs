using System.Collections.Concurrent;
using System.Security.Cryptography;
using Veldrath.Contracts.Auth;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Issues single-use opaque exchange codes that can be redeemed once for a full <see cref="AuthResponse"/>.
/// Codes are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> with background TTL expiry,
/// eliminating the TOCTOU race window that <c>IMemoryCache.TryGetValue</c> + <c>Remove</c> would introduce.
/// Each code is bound to the originating account — an intercepted code cannot be redeemed without
/// also knowing the correct <see cref="AuthResponse.AccountId"/>.
/// </summary>
public sealed class AuthExchangeCodeService : IDisposable
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (AuthResponse Response, DateTimeOffset ExpiresAt)> _codes = new();
    private readonly Timer _cleanup;

    /// <summary>Initializes a new instance of <see cref="AuthExchangeCodeService"/>.</summary>
    public AuthExchangeCodeService()
    {
        // Sweep expired entries every 30 seconds so memory doesn't accumulate.
        _cleanup = new Timer(_ => Sweep(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a short-lived opaque code bound to <paramref name="response"/> and
    /// <paramref name="accountId"/>. Returns a 64-character hex string valid for 60 seconds.
    /// </summary>
    public string CreateCode(AuthResponse response, Guid accountId)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _codes[CacheKey(code, accountId)] = (response, DateTimeOffset.UtcNow.Add(CodeTtl));
        return code;
    }

    /// <summary>
    /// Attempts to atomically consume a code issued for <paramref name="accountId"/> and retrieve
    /// the associated <see cref="AuthResponse"/>. <see cref="ConcurrentDictionary{TKey,TValue}.TryRemove"/>
    /// is a single atomic operation — no TOCTOU window exists between check and removal.
    /// Returns <c>false</c> if the code is unknown, expired, already consumed, or was issued for
    /// a different account.
    /// </summary>
    public bool TryConsume(string code, Guid accountId, out AuthResponse response)
    {
        var key = CacheKey(code, accountId);

        if (_codes.TryRemove(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            response = entry.Response;
            return true;
        }

        response = default!;
        return false;
    }

    /// <inheritdoc />
    public void Dispose() => _cleanup.Dispose();

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _codes)
        {
            if (kvp.Value.ExpiresAt <= now)
                _codes.TryRemove(kvp.Key, out _);
        }
    }

    private static string CacheKey(string code, Guid accountId) => $"auth:exchange:{code}:{accountId}";
}
