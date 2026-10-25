using System.Security.Cryptography;
using System.Text;

namespace Veldrath.Server.Features.Auth;

/// <summary>
/// Shared authorization helper for the internal bridge endpoints the SvelteKit server calls.
/// Requests must carry the secret configured in <c>InternalBridge:Token</c> in the
/// <c>X-Internal-Bridge-Token</c> header.
/// </summary>
internal static class InternalBridgeAuthorization
{
    /// <summary>Header carrying the shared internal bridge secret.</summary>
    internal const string BridgeTokenHeader = "X-Internal-Bridge-Token";

    /// <summary>Configuration key for the shared internal bridge secret.</summary>
    internal const string BridgeTokenConfigurationKey = "InternalBridge:Token";

    /// <summary>
    /// Validates the internal bridge token on the current request.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="config">Application configuration.</param>
    /// <param name="featureName">Feature name used in the "not configured" error message.</param>
    /// <returns>
    /// <see langword="null"/> when the request is authorized; otherwise the error result to return.
    /// </returns>
    internal static IResult? Validate(HttpContext context, IConfiguration config, string featureName)
    {
        var expected = config[BridgeTokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Results.Json(
                new { error = $"Internal {featureName} bridge is not configured." }, statusCode: 503);
        }

        var provided = context.Request.Headers[BridgeTokenHeader].ToString();
        if (string.IsNullOrEmpty(provided))
            return Results.Json(new { error = "Invalid internal bridge token." }, statusCode: 401);

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));

        return matches
            ? null
            : Results.Json(new { error = "Invalid internal bridge token." }, statusCode: 401);
    }
}
