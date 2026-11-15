using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Foundry;

/// <summary>
/// The single place an in-app notification is written.
/// </summary>
/// <remarks>
/// Two review paths produce notifications — a submission decision and a map decision — and they differ only in
/// their subject. Keeping the row construction in one method is what stops the second path from inventing a
/// slightly different message, a missing title, or an unset kind.
/// </remarks>
public class FoundryNotificationWriter(ApplicationDbContext db)
{
    /// <summary>Writes one notification to a recipient's inbox.</summary>
    /// <param name="recipientId">Account that will see the notification.</param>
    /// <param name="kind">What the notification is about.</param>
    /// <param name="title">Snapshot of the subject's title: the submission title or the map name.</param>
    /// <param name="message">Human-readable outcome, including any review notes.</param>
    /// <param name="submissionId">The submission, for a submission decision.</param>
    /// <param name="mapId">The map id, for a map decision.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the row is stored.</returns>
    public async Task NotifyAsync(
        Guid recipientId,
        FoundryNotificationKind kind,
        string title,
        string message,
        Guid? submissionId = null,
        string? mapId = null,
        CancellationToken cancellationToken = default)
    {
        db.FoundryNotifications.Add(new FoundryNotification
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            Kind = kind,
            SubmissionId = submissionId,
            MapId = mapId,
            // The column is 200 characters wide; a long submission title must not make the write fail.
            Title = title.Length > 200 ? title[..200] : title,
            Message = message.Length > 500 ? message[..500] : message,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
