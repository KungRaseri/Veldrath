using Veldrath.Contracts.Foundry;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Foundry;

/// <summary>
/// The one visibility rule for a Foundry submission, so the read path and the vote path cannot drift.
/// </summary>
/// <remarks>
/// A submission is visible to its author, to any reviewer, and to everyone once approved. Anything else is a
/// 404 (see <c>FoundryEndpoints.GetAsync</c>) — the row's existence is not disclosed, because a 403 would
/// confirm the id.
/// </remarks>
internal static class FoundryVisibility
{
    /// <summary>Whether <paramref name="callerId"/> may see <paramref name="submission"/>.</summary>
    /// <param name="callerId">The signed-in account.</param>
    /// <param name="isReviewer">Whether the account holds <c>content.review</c>.</param>
    /// <param name="submission">A loaded submission.</param>
    /// <returns><c>true</c> for the author, a reviewer, or anyone once the submission is approved.</returns>
    public static bool CanSee(Guid callerId, bool isReviewer, FoundrySubmission submission) =>
        CanSee(callerId, isReviewer, submission.SubmitterId, submission.Status == FoundrySubmissionStatus.Approved);

    /// <summary>Whether <paramref name="callerId"/> may see the submitted row described by a response DTO.</summary>
    /// <param name="callerId">The signed-in account.</param>
    /// <param name="isReviewer">Whether the account holds <c>content.review</c>.</param>
    /// <param name="submission">The detail DTO, whose status is the enum's name.</param>
    /// <returns><c>true</c> for the author, a reviewer, or anyone once the submission is approved.</returns>
    public static bool CanSee(Guid callerId, bool isReviewer, FoundrySubmissionDto submission) =>
        CanSee(
            callerId,
            isReviewer,
            submission.SubmitterId,
            submission.Status.Equals(nameof(FoundrySubmissionStatus.Approved), StringComparison.OrdinalIgnoreCase));

    private static bool CanSee(Guid callerId, bool isReviewer, Guid submitterId, bool isApproved) =>
        submitterId == callerId || isApproved || isReviewer;
}
