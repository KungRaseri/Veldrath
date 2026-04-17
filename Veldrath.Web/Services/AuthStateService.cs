using System.ComponentModel;
using Veldrath.Auth.Blazor;

namespace Veldrath.Web.Services;

/// <summary>
/// Scoped service that holds the authenticated player's JWT in circuit memory for Veldrath.Web.
/// Tokens never touch browser storage — they are only held for the lifetime of the Blazor
/// Server WebSocket circuit, preventing XSS-based token theft.
/// </summary>
/// <remarks>
/// The <see cref="AccessToken"/> and <see cref="RefreshToken"/> properties are surfaced here
/// for the SSR prerender initialisation pass, which requires direct token access to seed the
/// circuit state from the server-side HTTP context.  They are marked
/// <see cref="EditorBrowsableState.Never"/> to discourage accidental use in component code
/// — components should call <see cref="AuthStateServiceBase.TryRefreshAsync"/> or check
/// <see cref="AuthStateServiceBase.IsLoggedIn"/> instead.
/// </remarks>
public class AuthStateService(VeldrathApiClient api) : AuthStateServiceBase(api)
{
    /// <summary>
    /// The raw access token currently held in circuit memory.
    /// Exposed for SSR initialisation only — prefer <see cref="AuthStateServiceBase.IsLoggedIn"/>
    /// and <see cref="AuthStateServiceBase.TryRefreshAsync"/> in components.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? AccessToken => _accessToken;

    /// <summary>
    /// The raw refresh token currently held in circuit memory.
    /// Exposed for SSR initialisation only — do not pass this value to client-side code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? RefreshToken => _refreshToken;
}

