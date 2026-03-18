using MediatR;
using RealmEngine.Core.Features.SaveLoad;

namespace RealmEngine.Core.Features.Party.Queries;

/// <summary>
/// Query to get the current party composition.
/// </summary>
public record GetPartyQuery : IRequest<GetPartyResult>
{
}

/// <summary>
/// Result of getting party information.
/// </summary>
public record GetPartyResult
{
    /// <summary>
    /// Whether a party exists.
    /// </summary>
    public bool HasParty { get; init; }

    /// <summary>
    /// Party leader name.
    /// </summary>
    public string LeaderName { get; init; } = string.Empty;

    /// <summary>
    /// Party leader level.
    /// </summary>
    public int LeaderLevel { get; init; }

    /// <summary>
    /// Current party size (including leader).
    /// </summary>
    public int CurrentSize { get; init; }

    /// <summary>
    /// Maximum party size.
    /// </summary>
    public int MaxSize { get; init; }

    /// <summary>
    /// Party members.
    /// </summary>
    public List<PartyMemberInfo> Members { get; init; } = new();
}

/// <summary>
/// Party member info DTO.
/// </summary>
public record PartyMemberInfo
{
    /// <summary>
    /// Member ID.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Member name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Member class.
    /// </summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>
    /// Member level.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Current health.
    /// </summary>
    public int Health { get; init; }

    /// <summary>
    /// Maximum health.
    /// </summary>
    public int MaxHealth { get; init; }

    /// <summary>
    /// Party role.
    /// </summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Whether member is alive.
    /// </summary>
    public bool IsAlive { get; init; }
}

/// <summary>
/// Handler for GetPartyQuery.
/// </summary>
public class GetPartyHandler : IRequestHandler<GetPartyQuery, GetPartyResult>
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPartyHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GetPartyHandler(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Handles the get party query.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<GetPartyResult> Handle(GetPartyQuery request, CancellationToken cancellationToken)
    {
        var saveGame = _saveGameService.GetCurrentSave();

        if (saveGame == null || saveGame.Party == null)
        {
            return Task.FromResult(new GetPartyResult
            {
                HasParty = false
            });
        }

        var party = saveGame.Party;
        var members = party.Members.Select(m => new PartyMemberInfo
        {
            Id = m.Id,
            Name = m.Name,
            ClassName = m.ClassName,
            Level = m.Level,
            Health = m.Health,
            MaxHealth = m.MaxHealth,
            Role = m.Role.ToString(),
            IsAlive = m.IsAlive
        }).ToList();

        return Task.FromResult(new GetPartyResult
        {
            HasParty = true,
            LeaderName = party.Leader.Name,
            LeaderLevel = party.Leader.Level,
            CurrentSize = party.CurrentSize,
            MaxSize = party.MaxSize,
            Members = members
        });
    }
}
