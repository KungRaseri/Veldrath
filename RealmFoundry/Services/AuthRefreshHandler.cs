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

        return await base.SendAsync(request, cancellationToken);
    }
}
