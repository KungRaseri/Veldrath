namespace Veldrath.Server.Features.Foundry;

/// <summary>
/// Which community submissions a submission feed may include, derived from the caller's authority rather than
/// from a query string.
/// </summary>
/// <remarks>
/// The portal is public to signed-in accounts, so the feed cannot be "everything to everyone": an account that
/// cannot review sees its own work at any status and other people's only once a Curator has approved it and it
/// is therefore part of the catalog.
/// </remarks>
public enum FoundrySubmissionScope
{
    /// <summary>Every submission at every status. Reviewers only.</summary>
    Everything,

    /// <summary>Only the rows the caller submitted.</summary>
    Own,

    /// <summary>The caller's own rows at any status, plus everybody else's approved ones.</summary>
    OwnPlusApproved,
}
