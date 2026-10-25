using System.Security.Cryptography;
using System.Text;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Infrastructure;

/// <summary>
/// Registers ASP.NET Core Identity, JWT bearer authentication, authorization policies, and the
/// optional external OAuth providers used by the server API and SignalR hub.
/// </summary>
/// <remarks>
/// Extracted from <c>Program.cs</c> so the startup file only sequences the registrations. The
/// behaviour is unchanged: Identity first, then JWT bearer (so it overrides Identity's cookie
/// defaults), then authorization, then the OAuth providers that have credentials configured.
/// </remarks>
public static class AuthSetup
{
    /// <summary>
    /// Adds Identity, JWT bearer authentication, the permission policies, and any OAuth providers
    /// whose client ID and secret are configured.
    /// </summary>
    /// <param name="builder">The application builder whose configuration and services are used.</param>
    /// <returns>The names of the OAuth providers that were registered, in registration order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Jwt:Key</c>, <c>Jwt:Issuer</c>, or <c>Jwt:Audience</c> is missing or empty, so the
    /// process fails fast at startup instead of returning opaque 401s at runtime.
    /// </exception>
    public static string[] AddVeldrathIdentityAndAuth(this WebApplicationBuilder builder)
    {
        // AddIdentity must be registered before AddAuthentication so that the explicit
        // AddAuthentication call below can override Identity's default cookie schemes
        // with JWT Bearer (this is an API server, not a cookie-based web app).
        builder.Services.AddIdentity<PlayerAccount, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        AddJwtBearer(builder);
        AddAuthorizationPolicies(builder);

        var providers = AddOAuthProviders(builder);

        Log.Information(
            "OAuth providers configured: {Providers}",
            providers.Length == 0
                ? "none (external login endpoints will return 503 until credentials are set)"
                : string.Join(", ", providers));

        return providers;
    }

    /// <summary>
    /// Validates the JWT settings, logs a short key fingerprint, and registers JWT bearer
    /// authentication with the SignalR query-string token hook and the claims enrichment event.
    /// </summary>
    /// <param name="builder">The application builder whose configuration is read.</param>
    private static void AddJwtBearer(WebApplicationBuilder builder)
    {
        // Validate all required configuration values early so the app fails fast at startup
        // rather than producing opaque runtime errors.
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException("Jwt:Key is not configured or is empty.");

        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        if (string.IsNullOrWhiteSpace(jwtIssuer))
            throw new InvalidOperationException("Jwt:Issuer is not configured or is empty.");

        var jwtAudience = builder.Configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(jwtAudience))
            throw new InvalidOperationException("Jwt:Audience is not configured or is empty.");

        // Log a short fingerprint of the signing key (never the key itself) plus the issuer and
        // audience. Tokens minted by the SvelteKit front end are only accepted when all three
        // match, and a mismatched JWT_KEY between the two Coolify resources otherwise surfaces as
        // an opaque 401 on /api/characters. The web container logs the same fingerprint.
        Log.Information(
            "JWT configuration: issuer={Issuer} audience={Audience} keyFingerprint={Fingerprint}",
            jwtIssuer,
            jwtAudience,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jwtKey)))[..12]);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Allow SignalR to read the JWT from the query string (hub connections).
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) &&
                            ctx.Request.Path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                    // Identity-only tokens (minted by SvelteKit) are enriched with RBAC claims
                    // here; unknown accounts are provisioned just in time.
                    OnTokenValidated = JwtClaimsEnrichment.EnrichAsync,
                    // Make token rejections explainable: without this the API only returns an
                    // opaque 401 (signature key mismatch, wrong issuer/audience, expired token).
                    OnAuthenticationFailed = ctx =>
                    {
                        Log.Warning(
                            "JWT authentication failed: {Reason}",
                            ctx.Exception?.Message ?? "unknown reason");
                        return Task.CompletedTask;
                    },
                };
            });
    }

    /// <summary>Adds the legacy Curator role policy and one policy per fine-grained permission.</summary>
    /// <param name="builder">The application builder whose services receive the policies.</param>
    private static void AddAuthorizationPolicies(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization(options =>
        {
            // Legacy Curator role policy (kept for backward compat with existing Foundry endpoints).
            options.AddPolicy("Curator", p => p.RequireRole(Roles.Curator));

            // Fine-grained permission policies — each permission string maps to a JWT claim.
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, p => p.RequireClaim("permission", permission));
        });
    }

    /// <summary>
    /// Registers the OAuth providers that have credentials, so the server starts cleanly when they
    /// are absent (CI, local dev without OAuth app registrations).
    /// </summary>
    /// <param name="builder">The application builder whose configuration and services are used.</param>
    /// <returns>The provider names that were registered.</returns>
    /// <remarks>
    /// <c>SignInScheme</c> must be explicitly set to <see cref="IdentityConstants.ExternalScheme"/>
    /// so the OAuth middleware writes the external cookie that <c>GetExternalLoginInfoAsync</c>
    /// reads, and the correlation cookie must not require Secure on plain HTTP (Docker dev).
    /// </remarks>
    private static string[] AddOAuthProviders(WebApplicationBuilder builder)
    {
        var providers = new List<string>();

        // Compose maps OAUTH_* to OAuth__*; fall back to the .env names so a locally
        // sourced .env (set -a; source .env; dotnet run) also works.
        var discordId = builder.Configuration["OAuth:Discord:ClientId"]
            ?? Environment.GetEnvironmentVariable("OAUTH_DISCORD_CLIENT_ID");
        var discordSecret = builder.Configuration["OAuth:Discord:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("OAUTH_DISCORD_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(discordId) && !string.IsNullOrEmpty(discordSecret))
        {
            builder.Services.AddAuthentication()
                .AddDiscord(o =>
                {
                    o.SignInScheme = IdentityConstants.ExternalScheme;
                    o.ClientId = discordId;
                    o.ClientSecret = discordSecret;
                    o.Scope.Add("email");
                    o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
                    // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                });
            providers.Add("Discord");
        }

        var googleId = builder.Configuration["OAuth:Google:ClientId"]
            ?? Environment.GetEnvironmentVariable("OAUTH_GOOGLE_CLIENT_ID");
        var googleSecret = builder.Configuration["OAuth:Google:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("OAUTH_GOOGLE_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(googleId) && !string.IsNullOrEmpty(googleSecret))
        {
            builder.Services.AddAuthentication()
                .AddGoogle(o =>
                {
                    o.SignInScheme = IdentityConstants.ExternalScheme;
                    o.ClientId = googleId;
                    o.ClientSecret = googleSecret;
                    o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
                    // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                });
            providers.Add("Google");
        }

        var microsoftId = builder.Configuration["OAuth:Microsoft:ClientId"]
            ?? Environment.GetEnvironmentVariable("OAUTH_MICROSOFT_CLIENT_ID");
        var microsoftSecret = builder.Configuration["OAuth:Microsoft:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("OAUTH_MICROSOFT_CLIENT_SECRET");
        if (!string.IsNullOrEmpty(microsoftId) && !string.IsNullOrEmpty(microsoftSecret))
        {
            builder.Services.AddAuthentication()
                .AddMicrosoftAccount(o =>
                {
                    o.SignInScheme = IdentityConstants.ExternalScheme;
                    o.ClientId = microsoftId;
                    o.ClientSecret = microsoftSecret;
                    // /consumers/ targets personal Microsoft accounts (Xbox/Live/Outlook) only,
                    // which avoids the userAudience mismatch that /common/ produces with a
                    // single-tenant app registration.
                    o.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
                    o.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
                    o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
                    // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                    o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                });
            providers.Add("Microsoft");
        }

        return [.. providers];
    }
}
