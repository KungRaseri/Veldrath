using Veldrath.Contracts.Auth;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Auth;

/// <summary>Outcome of an external OAuth login or register attempt.</summary>
public enum ExternalLoginStatus
{
    /// <summary>Authentication succeeded and tokens have been issued.</summary>
    Success,

    /// <summary>Authentication failed due to a server-side or Identity error.</summary>
    Error,

    /// <summary>
    /// A provider-link confirmation email has been sent.
    /// The response cannot be issued until the user clicks the confirmation link.
    /// </summary>
    PendingLinkConfirmation,
}

/// <summary>
/// Returned by <see cref="AuthService.ExternalLoginOrRegisterAsync"/> to indicate
/// whether a session was issued, an error occurred, or a confirmation email was sent.
/// </summary>
/// <param name="Response">Populated on <see cref="ExternalLoginStatus.Success"/>; otherwise <see langword="null"/>.</param>
/// <param name="Error">Human-readable error detail on <see cref="ExternalLoginStatus.Error"/>; otherwise <see langword="null"/>.</param>
/// <param name="Status">Machine-readable outcome code.</param>
/// <param name="PendingLinkAccount">
/// The existing <see cref="PlayerAccount"/> whose email matched the incoming provider.
/// Populated on <see cref="ExternalLoginStatus.PendingLinkConfirmation"/>; otherwise <see langword="null"/>.
/// </param>
public record ExternalLoginResult(
    AuthResponse?   Response,
    string?         Error,
    ExternalLoginStatus Status,
    PlayerAccount?  PendingLinkAccount = null);
