using Veldrath.Server.Features.Party;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Stub <see cref="IPartyCombatQuery"/>: hands back a canned member list, so combat tests can decide who is
/// standing next to a character without seeding parties, characters and live sessions.
/// </summary>
public sealed class FakePartyCombatQuery : IPartyCombatQuery
{
    /// <summary>Gets the members returned for every lookup.</summary>
    public List<Guid> Members { get; } = [];

    /// <summary>Gets the character ids that were looked up, in call order.</summary>
    public List<Guid> Queried { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetOnlinePartyMembersAtLocationAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        Queried.Add(characterId);
        return Task.FromResult<IReadOnlyList<Guid>>(Members.ToList());
    }
}
