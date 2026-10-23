using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Payload sent by the SvelteKit front end to mirror a Better Auth identity into
/// ASP.NET Core Identity. Used only during the Blazor Foundry transition.
/// </summary>
/// <param name="AccountId">Better Auth user ID, which is also the game account GUID.</param>
/// <param name="Username">Preferred username for the Identity account.</param>
/// <param name="Email">Email address held by Better Auth.</param>
/// <param name="EmailVerified">Whether Better Auth considers the email verified.</param>
/// <param name="Password">
/// Optional plaintext password captured at registration or password change. It is hashed with
/// Identity's password hasher so Blazor Foundry's existing login endpoint keeps working.
/// </param>
/// <param name="ExternalLogins">Optional linked OAuth providers so Foundry's OAuth login finds the account.</param>
public sealed record InternalAccountSyncRequest(
    Guid AccountId,
    string? Username,
    string? Email,
    bool EmailVerified,
    string? Password,
    IReadOnlyList<InternalExternalLogin>? ExternalLogins);

/// <summary>An OAuth login linked to a Better Auth account.</summary>
/// <param name="Provider">Better Auth provider ID, for example <c>discord</c>.</param>
/// <param name="ProviderKey">Provider-issued subject identifier.</param>
/// <param name="DisplayName">Optional provider display name.</param>
public sealed record InternalExternalLogin(string Provider, string ProviderKey, string? DisplayName);

/// <summary>Result of an internal account sync.</summary>
/// <param name="AccountId">Game account GUID.</param>
/// <param name="Username">Resolved Identity username.</param>
/// <param name="PasswordSynced">Whether a password hash was written.</param>
/// <param name="LinkedLogins">Providers that are linked after the sync completes.</param>
public sealed record InternalAccountSyncResponse(
    Guid AccountId,
    string Username,
    bool PasswordSynced,
    IReadOnlyList<string> LinkedLogins);

/// <summary>
/// Temporary internal endpoints used by the SvelteKit front end to keep ASP.NET Core
/// Identity accounts in sync while Blazor Foundry and the desktop client still authenticate
/// through the .NET auth endpoints.
/// <para>
/// Requests must present the shared secret from <c>InternalBridge:Token</c> in the
/// <c>X-Internal-Bridge-Token</c> header. The endpoints are intentionally not exposed to
/// browsers and are removed when Foundry moves into SvelteKit.
/// </para>
/// </summary>
public static class InternalAccountBridgeEndpoints
{
    /// <summary>Header carrying the shared internal bridge secret.</summary>
    public const string BridgeTokenHeader = "X-Internal-Bridge-Token";

    /// <summary>Configuration key for the shared internal bridge secret.</summary>
    public const string BridgeTokenConfigurationKey = "InternalBridge:Token";

    private static readonly Dictionary<string, string> ProviderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["discord"] = "Discord",
        ["google"] = "Google",
        ["microsoft"] = "Microsoft",
    };

    /// <summary>Maps the internal account bridge endpoints.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapInternalAccountBridgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/internal/accounts").WithTags("Internal");

        group.MapPost("/sync", SyncAsync);

        return app;
    }

    private static async Task<IResult> SyncAsync(
        InternalAccountSyncRequest request,
        HttpContext context,
        IConfiguration config,
        AccountProvisioningService provisioning,
        UserManager<PlayerAccount> userManager,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(InternalAccountBridgeEndpoints));

        if (request.AccountId == Guid.Empty)
            return Results.BadRequest(new { error = "AccountId is required." });

        var expectedToken = config[BridgeTokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(expectedToken))
            return Results.Json(
                new { error = "Internal account bridge is not configured." }, statusCode: 503);

        if (!IsAuthorized(context, expectedToken))
            return Results.Json(new { error = "Invalid internal bridge token." }, statusCode: 401);

        var snapshot = await provisioning.EnsureAccountAsync(
            request.AccountId, request.Username, request.Email, request.EmailVerified, ct);

        if (snapshot is null)
            return Results.Json(new { error = "Account provisioning failed." }, statusCode: 500);

        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null)
            return Results.Json(new { error = "Account provisioning failed." }, statusCode: 500);

        var passwordSynced = false;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            // Hash directly instead of AddPasswordAsync: the bridge must not require the
            // current password (Better Auth owns it) and must overwrite an existing hash.
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.Password);
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                logger.LogError(
                    "Internal account sync failed to store a password hash for {AccountId}: {Errors}",
                    request.AccountId, string.Join("; ", update.Errors.Select(e => e.Description)));
                return Results.Json(new { error = "Password sync failed." }, statusCode: 500);
            }

            passwordSynced = true;
        }

        var linkedLogins = await LinkExternalLoginsAsync(
            user, request.ExternalLogins, userManager, logger, ct);

        AccountProvisioningService.ClearCache();

        return Results.Ok(new InternalAccountSyncResponse(
            user.Id,
            user.UserName ?? request.AccountId.ToString(),
            passwordSynced,
            linkedLogins));
    }

    private static async Task<List<string>> LinkExternalLoginsAsync(
        PlayerAccount user,
        IReadOnlyList<InternalExternalLogin>? logins,
        UserManager<PlayerAccount> userManager,
        ILogger logger,
        CancellationToken ct)
    {
        var linked = new List<string>();
        if (logins is not { Count: > 0 })
            return linked;

        foreach (var login in logins)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(login.Provider) || string.IsNullOrWhiteSpace(login.ProviderKey))
                continue;

            var provider = NormalizeProvider(login.Provider);

            var existing = await userManager.FindByLoginAsync(provider, login.ProviderKey);
            if (existing is not null)
            {
                if (!linked.Contains(provider, StringComparer.Ordinal))
                    linked.Add(provider);
                continue;
            }

            var info = new UserLoginInfo(provider, login.ProviderKey, login.DisplayName ?? provider);
            var result = await userManager.AddLoginAsync(user, info);
            if (result.Succeeded)
            {
                if (!linked.Contains(provider, StringComparer.Ordinal))
                    linked.Add(provider);
                continue;
            }

            logger.LogWarning(
                "Internal account sync could not link {Provider} for {AccountId}: {Errors}",
                provider, user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return linked;
    }

    /// <summary>
    /// Maps Better Auth's lowercase provider IDs to the Identity provider names used by the
    /// existing .NET OAuth flows and <c>AspNetUserLogins</c> rows.
    /// </summary>
    internal static string NormalizeProvider(string provider) =>
        ProviderAliases.TryGetValue(provider, out var canonical) ? canonical : provider;

    private static bool IsAuthorized(HttpContext context, string expected)
    {
        var provided = context.Request.Headers[BridgeTokenHeader].ToString();
        if (string.IsNullOrEmpty(provided))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
