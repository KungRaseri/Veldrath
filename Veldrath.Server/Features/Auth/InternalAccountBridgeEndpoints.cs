using Microsoft.AspNetCore.Identity;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Payload sent by the SvelteKit front end to mirror a Better Auth identity into
/// ASP.NET Core Identity. Retained while the desktop client's native fallback signs in
/// through Identity.
/// </summary>
/// <param name="AccountId">Better Auth user ID, which is also the game account GUID.</param>
/// <param name="Username">Preferred username for the Identity account.</param>
/// <param name="Email">Email address held by Better Auth.</param>
/// <param name="EmailVerified">Whether Better Auth considers the email verified.</param>
/// <param name="Password">
/// Optional plaintext password captured at registration or password change. It is hashed with
/// Identity's password hasher so the desktop client's native sign-in keeps working.
/// </param>
/// <param name="ExternalLogins">Optional linked OAuth providers so Foundry's OAuth login finds the account.</param>
/// <param name="DisplayName">Optional display name owned by Better Auth; mirrored to <c>PlayerAccount.DisplayName</c>.</param>
public sealed record InternalAccountSyncRequest(
    Guid AccountId,
    string? Username,
    string? Email,
    bool EmailVerified,
    string? Password,
    IReadOnlyList<InternalExternalLogin>? ExternalLogins,
    string? DisplayName = null);

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
/// Identity accounts in sync while the desktop client still authenticates through the
/// .NET auth endpoints.
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

        if (InternalBridgeAuthorization.Validate(context, config, "account") is { } failure)
            return failure;

        var snapshot = await provisioning.EnsureAccountAsync(
            request.AccountId, request.Username, request.Email, request.EmailVerified, ct);

        if (snapshot is null)
            return Results.Json(new { error = "Account provisioning failed." }, statusCode: 500);

        var user = await userManager.FindByIdAsync(request.AccountId.ToString());
        if (user is null)
            return Results.Json(new { error = "Account provisioning failed." }, statusCode: 500);

        if (!await ApplyIdentityUpdateAsync(user, request, userManager, logger))
            return Results.Json(new { error = "Account identity sync failed." }, statusCode: 500);

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

    /// <summary>
    /// Mirrors username, email, verification, and display-name changes from Better Auth onto
    /// the existing Identity row. JIT provisioning only creates rows, so updates must be
    /// applied explicitly on every sync.
    /// </summary>
    private static async Task<bool> ApplyIdentityUpdateAsync(
        PlayerAccount user,
        InternalAccountSyncRequest request,
        UserManager<PlayerAccount> userManager,
        ILogger logger)
    {
        var changed = false;

        var username = request.Username?.Trim();
        if (!string.IsNullOrWhiteSpace(username)
            && !string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            var result = await userManager.SetUserNameAsync(user, username);
            if (result.Succeeded)
            {
                changed = true;
            }
            else
            {
                logger.LogWarning(
                    "Internal account sync could not update the username for {AccountId}: {Errors}",
                    request.AccountId, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        var email = request.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email)
            && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var result = await userManager.SetEmailAsync(user, email);
            if (result.Succeeded)
            {
                changed = true;
            }
            else
            {
                logger.LogWarning(
                    "Internal account sync could not update the email for {AccountId}: {Errors}",
                    request.AccountId, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        var emailMatches = string.IsNullOrWhiteSpace(email)
            || string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailMatches && user.EmailConfirmed != request.EmailVerified)
        {
            user.EmailConfirmed = request.EmailVerified;
            changed = true;
        }

        var displayName = request.DisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName)
            && !string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
        {
            user.DisplayName = displayName;
            changed = true;
        }

        if (!changed)
            return true;

        var update = await userManager.UpdateAsync(user);
        if (update.Succeeded)
            return true;

        logger.LogError(
            "Internal account sync failed to update the identity for {AccountId}: {Errors}",
            request.AccountId, string.Join("; ", update.Errors.Select(e => e.Description)));
        return false;
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

}
