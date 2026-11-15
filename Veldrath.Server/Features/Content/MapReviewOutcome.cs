using Veldrath.Contracts.Map;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The result of a review decision, plus what the caller needs to tell the author about it.
/// </summary>
/// <remarks>
/// The transport response deliberately carries only the outcome. The author's account and the map's name are here
/// because the endpoint — not the response body — is what writes the notification and the audit entry.
/// </remarks>
/// <param name="Response">What the authoring API returns.</param>
/// <param name="AuthorAccountId">
/// Account to notify: whoever submitted the map, or whoever created it when the submission stamp has been cleared.
/// <see langword="null"/> for seeded content, which nobody submits.
/// </param>
/// <param name="MapName">The map's name at the moment of the decision, snapshotted into the notification.</param>
public record MapReviewOutcome(
    MapLifecycleResponse Response,
    Guid? AuthorAccountId,
    string MapName);
