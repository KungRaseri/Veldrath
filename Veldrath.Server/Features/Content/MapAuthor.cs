namespace Veldrath.Server.Features.Content;

/// <summary>
/// The account to tell about a map lifecycle change, with the map's name for the notification title.
/// </summary>
/// <remarks>
/// A review outcome already carries both facts, which is why approval and rejection notify without a second
/// query. Deactivation had neither, so the notification it writes needs them from somewhere: the submitter,
/// falling back to the creator for a map that was approved without a submission, and the name, because a title
/// made of a map id is unreadable in an inbox.
/// </remarks>
/// <param name="AccountId">The submitter or creator, or <see langword="null"/> for seeded content.</param>
/// <param name="Name">The map name, snapshotted into the notification title.</param>
public readonly record struct MapAuthor(Guid? AccountId, string Name);
