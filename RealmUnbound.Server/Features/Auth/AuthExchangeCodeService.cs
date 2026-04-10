using Microsoft.Extensions.Caching.Memory;
using RealmUnbound.Contracts.Auth;
using System.Security.Cryptography;

namespace RealmUnbound.Server.Features.Auth;

/// <summary>
/// Issues single-use opaque exchange codes that can be redeemed once for a full <see cref="AuthResponse"/>.
/// Codes are stored in <see cref="IMemoryCache"/> with a 60-second TTL and are invalidated on first use,
/// preventing replay attacks or token harvesting from browser history and server access logs.
/// </summary>
public sealed class AuthExchangeCodeService(IMemoryCache cache)
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Creates a short-lived opaque code mapped to the provided auth response.
    /// Returns a 64-character hex string that maps to <paramref name="response"/> for up to 60 seconds.
    /// </summary>
    public string CreateCode(AuthResponse response)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        cache.Set(CacheKey(code), response, CodeTtl);
        return code;
    }

    /// <summary>
    /// Attempts to consume a code and retrieve the associated <see cref="AuthResponse"/>.
    /// Returns <c>false</c> if the code is unknown, expired, or already consumed.
    /// </summary>
    public bool TryConsume(string code, out AuthResponse response)
    {
        var key = CacheKey(code);
        if (cache.TryGetValue(key, out AuthResponse? stored) && stored is not null)
        {
            cache.Remove(key); // single-use: invalidate immediately after first read
            response = stored;
            return true;
        }

        response = default!;
        return false;
    }

    private static string CacheKey(string code) => $"auth:exchange:{code}";
}
