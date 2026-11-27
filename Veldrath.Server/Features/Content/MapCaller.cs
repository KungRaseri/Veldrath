using RealmEngine.Data.Entities;
using MapEntity = RealmEngine.Data.Entities.Map;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// Who is asking for a map operation, and how far their authority reaches.
/// </summary>
/// <remarks>
/// The portal is readable by every signed-in account, so a permission alone cannot decide a write: a map
/// belongs to the account that created it (<c>MapEntity.CreatedByAccountId</c>), and <c>content.manage</c> may
/// write any map. A reviewer reads every draft because deciding on it is the job.
/// </remarks>
/// <param name="AccountId">The signed-in account.</param>
/// <param name="CanManageAny">Whether the account holds <c>content.manage</c>.</param>
/// <param name="CanReview">Whether the account holds <c>content.review</c>.</param>
public readonly record struct MapCaller(Guid AccountId, bool CanManageAny, bool CanReview)
{
    /// <summary>Whether the caller may see drafts they did not write.</summary>
    public bool SeesEveryDraft => CanManageAny || CanReview;

    /// <summary>Whether the caller may write a map created by <paramref name="createdByAccountId"/>.</summary>
    /// <param name="createdByAccountId">The map's author, or <c>null</c> for seeded content.</param>
    /// <returns><c>true</c> for an editor, or for the account that created the map.</returns>
    public bool MayEdit(Guid? createdByAccountId) =>
        CanManageAny || (createdByAccountId is { } author && author == AccountId);

    /// <summary>Whether the caller may see a map at all. The one place the map visibility rule lives.</summary>
    /// <remarks>
    /// A live map is public; a draft belongs to its author until a reviewer approves it. Every read path calls
    /// this, so a picker cannot list what a detail read would hide.
    /// </remarks>
    /// <param name="isActive">Whether a reviewer has approved the map, so the game shows it.</param>
    /// <param name="createdByAccountId">The map's author, or <c>null</c> for seeded content.</param>
    /// <returns><c>true</c> for a live map, a reviewer, or the account that created the map.</returns>
    public bool CanSee(bool isActive, Guid? createdByAccountId) =>
        isActive || SeesEveryDraft || MayEdit(createdByAccountId);

    /// <summary>Whether the caller may see <paramref name="map"/>; the rule is in the other overload.</summary>
    /// <param name="map">A loaded map.</param>
    /// <returns><c>true</c> when the caller is allowed to read it.</returns>
    public bool CanSee(MapEntity map) => CanSee(map.IsActive, map.CreatedByAccountId);
}
