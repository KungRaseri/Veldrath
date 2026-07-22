using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Veldrath.Contracts.Auth;

namespace Veldrath.Auth.Blazor;

/// <summary>
/// A <see cref="DelegatingHandler"/> that intercepts 401 Unauthorized responses,
/// attempts a one-time JWT renewal using the stored refresh token, and retries
/// the original request with the fresh token. Acts as a reactive safety net for
/// the proactive <see cref="AuthStateServiceBase.TryRefreshAsync"/> pattern.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>scoped</b> so the <see cref="SemaphoreSlim"/> serialises
/// refresh attempts within a single Blazor Server circuit without blocking other
/// users. The renew call uses a separate named <c>HttpClient</c>
/// (<c>"veldrath-web-raw"</c>) to avoid re-entering this handler.
/// </para>
/// <para>
/// Configuration is read from <c>Auth:RefreshHandler:Enabled</c> (master
/// kill-switch, default <c>true</c>) and <c>Auth:RefreshHandler:RenewTimeoutSeconds</c>
/// (hard timeout for the renew HTTP call, default 10).
/// </para>
/// </remarks>
public sealed class AuthDelegatingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthDelegatingHandler> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// Tracks the access token value that was in use when the most recent 401 was
    /// received. Used for double-check detection after acquiring <see cref="_refreshLock"/>:
    /// if the current token differs, another concurrent request already refreshed it.
    /// </summary>
    private string? _lastFailedAccessToken;

    /// <summary>
    /// Guards against re-entrancy when <see cref="AuthStateServiceBase.LogOutAsync"/> is
    /// called during auth state cleanup after a renewal failure. Prevents the handler from
    /// intercepting the logout HTTP request itself.
    /// </summary>
    private bool _isClearingAuthState;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthDelegatingHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to lazily resolve
    /// <see cref="AuthStateServiceBase"/> during request processing, avoiding a
    /// <see cref="Lazy{T}"/> circular dependency in the HttpClientFactory pipeline.</param>
    /// <param name="httpClientFactory">Factory for creating the raw <c>HttpClient</c> used by renew calls.</param>
    /// <param name="configuration">Provides <c>Auth:RefreshHandler</c> settings.</param>
    /// <param name="logger">Structured logger for diagnostic messages.</param>
    public AuthDelegatingHandler(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AuthDelegatingHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts HTTP responses. On 401, attempts a one-time JWT renewal and retries
    /// the original request. On refresh failure, propagates the original 401.
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message, either the original, a retried success, or a propagated 401.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Lazy-resolve AuthStateServiceBase to break the Lazy<T> circular dependency:
        // AuthStateService → VeldrathApiClient → HttpClient("veldrath-web") → AuthDelegatingHandler → AuthStateServiceBase → (cycle)
        // By resolving here instead of in the constructor, we defer until the DI container is fully initialized.
        var authState = _serviceProvider.GetRequiredService<AuthStateServiceBase>();

        var response = await base.SendAsync(request, cancellationToken);

        if (!ShouldIntercept(request, response))
            return response;

        // Read the failed token before acquiring the lock so we can detect
        // whether another concurrent request already refreshed it.
        var failedToken = request.Headers.Authorization?.Parameter;
        _lastFailedAccessToken = failedToken;

        _logger.LogDebug(
            "AuthDelegatingHandler intercepted 401 for {Method} {Path}. Attempting token refresh.",
            request.Method, request.RequestUri?.AbsolutePath);

        if (!await _refreshLock.WaitAsync(GetLockTimeout(cancellationToken), cancellationToken))
        {
            _logger.LogWarning(
                "AuthDelegatingHandler timed out waiting for refresh lock for {Method} {Path}.",
                request.Method, request.RequestUri?.AbsolutePath);
            return response;
        }

        try
        {
            // Double-check: did another request already refresh the token while we waited?
            if (authState.AccessToken is not null &&
                !string.Equals(authState.AccessToken, failedToken, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Token was already refreshed by another concurrent request. Retrying with current token.");
                return await RetryWithFreshTokenAsync(request, cancellationToken);
            }

            var renewed = await RenewJwtAsync(cancellationToken);
            if (renewed is null)
            {
                _logger.LogWarning(
                    "JWT renewal failed for {Method} {Path}. Clearing stale auth state and propagating 401.",
                    request.Method, request.RequestUri?.AbsolutePath);

                _isClearingAuthState = true;
                try
                {
                    await authState.LogOutAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error clearing auth state after JWT renewal failure for {Method} {Path}.",
                        request.Method, request.RequestUri?.AbsolutePath);
                }
                finally
                {
                    _isClearingAuthState = false;
                }

                return response;
            }

            // Update circuit auth state — this also calls SetBearerToken on the scoped HttpClient.
            await authState.SetTokensAsync(renewed, authState.RefreshToken!);

            _logger.LogInformation(
                "JWT renewed successfully. Retrying {Method} {Path} with fresh token.",
                request.Method, request.RequestUri?.AbsolutePath);

            return await RetryWithFreshTokenAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "AuthDelegatingHandler refresh cancelled for {Method} {Path}.",
                request.Method, request.RequestUri?.AbsolutePath);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during JWT refresh for {Method} {Path}. Propagating original 401.",
                request.Method, request.RequestUri?.AbsolutePath);
            return response;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Determines whether the handler should intercept this response for JWT refresh.
    /// Returns <c>false</c> for the renew endpoint itself, non-401 status codes,
    /// requests lacking an Authorization header, already-retried requests, and when
    /// auth state is already being cleared (re-entrancy guard).
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="response">The received HTTP response message.</param>
    /// <returns><c>true</c> if the handler should attempt a token refresh; otherwise <c>false</c>.</returns>
    private bool ShouldIntercept(HttpRequestMessage request, HttpResponseMessage response)
    {
        // Re-entrancy guard: when we're already clearing stale auth state (e.g. after
        // a renewal failure), don't intercept any requests — including the logout call
        // itself — to avoid loops and deadlocks on the refresh semaphore.
        if (_isClearingAuthState)
            return false;

        // Never intercept the renew endpoint itself (defense in depth).
        if (request.RequestUri?.AbsolutePath.EndsWith("/api/auth/renew-jwt",
                StringComparison.OrdinalIgnoreCase) == true)
            return false;

        // Only intercept 401 Unauthorized.
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return false;

        // Only intercept requests that carried an Authorization header.
        // Unauthenticated requests receiving 401 is expected behavior.
        if (request.Headers.Authorization is null)
            return false;

        // Don't intercept an already-retried request (loop prevention).
        if (request.Headers.Contains("X-Auth-Retry"))
            return false;

        return true;
    }

    /// <summary>
    /// Calls <c>POST /api/auth/renew-jwt</c> with the stored refresh token using a raw
    /// <see cref="HttpClient"/> that bypasses this handler. Returns the
    /// <see cref="RenewJwtResponse"/> on success, or <see langword="null"/> on failure.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The renewal response, or <see langword="null"/> if the call failed or was disabled.</returns>
    private async Task<RenewJwtResponse?> RenewJwtAsync(CancellationToken ct)
    {
        // Master kill-switch.
        if (!_configuration.GetValue("Auth:RefreshHandler:Enabled", defaultValue: true))
        {
            _logger.LogDebug("AuthDelegatingHandler is disabled via Auth:RefreshHandler:Enabled.");
            return null;
        }

        var authState = _serviceProvider.GetRequiredService<AuthStateServiceBase>();
        var refreshToken = authState.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogDebug("No refresh token available in auth state — cannot renew JWT.");
            return null;
        }

        var renewTimeout = TimeSpan.FromSeconds(
            _configuration.GetValue("Auth:RefreshHandler:RenewTimeoutSeconds", defaultValue: 10));

        using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        renewCts.CancelAfter(renewTimeout);

        try
        {
            var rawClient = _httpClientFactory.CreateClient("veldrath-web-raw");
            var response = await rawClient.PostAsJsonAsync(
                "/api/auth/renew-jwt",
                new { RefreshToken = refreshToken },
                renewCts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync<RenewJwtResponse>(renewCts.Token);
                return result;
            }

            _logger.LogWarning(
                "RenewJwtAsync returned {StatusCode} {Reason}. Refresh token may be expired or revoked.",
                (int)response.StatusCode, response.ReasonPhrase);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "RenewJwtAsync timed out after {TimeoutSeconds}s.",
                renewTimeout.TotalSeconds);
            return null;
        }
    }

    /// <summary>
    /// Clones the original request, applies the current access token from the
    /// lazily-resolved <see cref="AuthStateServiceBase"/>, marks it with
    /// <c>X-Auth-Retry: 1</c> to prevent re-interception, and sends it through
    /// the inner handler pipeline.
    /// </summary>
    /// <param name="original">The original request that received a 401.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response from the retried request.</returns>
    private async Task<HttpResponseMessage> RetryWithFreshTokenAsync(
        HttpRequestMessage original, CancellationToken cancellationToken)
    {
        var retry = await CloneRequestAsync(original);

        var authState = _serviceProvider.GetRequiredService<AuthStateServiceBase>();
        var currentToken = authState.AccessToken;
        if (currentToken is not null)
        {
            retry.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", currentToken);
        }

        retry.Headers.Add("X-Auth-Retry", "1");

        _logger.LogDebug(
            "Retrying {Method} {Path} with fresh token (X-Auth-Retry: 1).",
            retry.Method, retry.RequestUri?.AbsolutePath);

        return await base.SendAsync(retry, cancellationToken);
    }

    /// <summary>
    /// Creates a deep clone of an <see cref="HttpRequestMessage"/> so the original
    /// request can be retried with updated headers. The request content stream is
    /// re-read if present.
    /// </summary>
    /// <param name="original">The original request to clone.</param>
    /// <returns>A new <see cref="HttpRequestMessage"/> with the same method, URI, and content.</returns>
    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            if (original.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }

        // Copy headers from the original (except Authorization, which we'll set fresh).
        foreach (var header in original.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    /// <summary>
    /// Computes the lock acquisition timeout. Uses the request's cancellation token
    /// deadline if one exists, otherwise falls back to a 10-second hard timeout.
    /// </summary>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A <see cref="TimeSpan"/> representing the maximum time to wait for the lock.</returns>
    private static TimeSpan GetLockTimeout(CancellationToken cancellationToken)
    {
        // If the caller already has a timeout, respect it minus a small buffer.
        // Otherwise use a hard 10-second timeout.
        return TimeSpan.FromSeconds(10);
    }
}
