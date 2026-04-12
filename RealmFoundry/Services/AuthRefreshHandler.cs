namespace RealmFoundry.Services;

/// <summary>
/// Blazor HttpClient DelegatingHandler that proactively refreshes the JWT access
/// token when it is about to expire, before forwarding the request to the server.
/// Skip /api/auth/ paths to prevent circular calls during the refresh itself.
/// </summary>
/// <remarks>
/// Injects <see cref="IServiceProvider"/> rather than <see cref="AuthStateService"/> directly
/// to break the circular dependency:
/// RealmFoundryApiClient → AuthRefreshHandler → AuthStateService → RealmFoundryApiClient.
/// AuthStateService is resolved lazily inside SendAsync, after all services are constructed.
/// </remarks>
public sealed class AuthRefreshHandler(IServiceProvider services) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never intercept auth endpoints — that would cause infinite recursion.
        if (request.RequestUri?.AbsolutePath.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) == true)
            return await base.SendAsync(request, cancellationToken);

        var auth = services.GetRequiredService<AuthStateService>();
        if (auth.IsLoggedIn && auth.TokenExpiresSoon)
            await auth.TryRefreshAsync();

        // Stamp the bearer token on every outgoing request. This ensures component-injected
        // RealmFoundryApiClient instances (which are transient and start with no headers)
        // are authenticated without requiring each component to call SetBearerToken.
        if (auth.IsLoggedIn && auth.AccessToken is not null)
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
