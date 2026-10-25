using MediatR;
using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Party;

/// <summary>Result of any party command: the new snapshot, a refusal, or a disband.</summary>
public sealed record PartyCommandResult
{
    /// <summary>Gets a value indicating whether the command was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the reason a command was refused.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the party snapshot after the change, or <see langword="null"/> when the caller is now solo.</summary>
    public PartySnapshot? Party { get; init; }

    /// <summary>
    /// Gets the party the command acted on, so the hub can move the connection between SignalR groups even when the
    /// caller ends up with no party (a disband, or leaving a party that still exists).
    /// </summary>
    public Guid? PartyId { get; init; }

    /// <summary>Gets a value indicating whether the party ceased to exist because its last member left.</summary>
    public bool PartyDisbanded { get; init; }
}

/// <summary>Creates a party led by the calling character.</summary>
/// <param name="CharacterId">Character creating the party.</param>
public record CreatePartyHubCommand(Guid CharacterId) : IRequest<PartyCommandResult>;

/// <summary>Removes the calling character from their party.</summary>
/// <param name="CharacterId">Character leaving.</param>
public record LeavePartyHubCommand(Guid CharacterId) : IRequest<PartyCommandResult>;

/// <summary>Removes another character from the leader's party.</summary>
/// <param name="LeaderCharacterId">Character performing the kick; must be the leader.</param>
/// <param name="TargetCharacterId">Character being removed.</param>
public record KickPartyMemberHubCommand(Guid LeaderCharacterId, Guid TargetCharacterId) : IRequest<PartyCommandResult>;

/// <summary>Reads the caller's party, or reports that they are solo.</summary>
/// <param name="CharacterId">Character whose party to read.</param>
public record GetPartyHubQuery(Guid CharacterId) : IRequest<PartyCommandResult>;

/// <summary>
/// Handles <see cref="CreatePartyHubCommand"/>. Refuses when the character is already grouped; the unique index on
/// <c>PartyMembers.CharacterId</c> is translated into the same refusal instead of surfacing a database error.
/// </summary>
public class CreatePartyHubCommandHandler : IRequestHandler<CreatePartyHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;
    private readonly ILogger<CreatePartyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="CreatePartyHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    /// <param name="logger">Logger instance.</param>
    public CreatePartyHubCommandHandler(PartyStore parties, ILogger<CreatePartyHubCommandHandler> logger)
    {
        _parties = parties;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        CreatePartyHubCommand request,
        CancellationToken cancellationToken)
    {
        if (await _parties.GetPartyForCharacterAsync(request.CharacterId, cancellationToken) is not null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are already in a party" };
        }

        try
        {
            var party = await _parties.CreateAsync(request.CharacterId, cancellationToken);
            _logger.LogInformation(
                "Character {CharacterIdPrefix} created party {PartyId}",
                request.CharacterId.ToString()[..8],
                party.Id);

            return new PartyCommandResult
            {
                Success = true,
                Party = await _parties.ToSnapshotAsync(party, cancellationToken),
            };
        }
        catch (DbUpdateException)
        {
            // Lost a race with another claim of the same character — the unique index is the authority here.
            return new PartyCommandResult { Success = false, ErrorMessage = "You are already in a party" };
        }
    }
}

/// <summary>
/// Handles <see cref="LeavePartyHubCommand"/>. The leader leaving promotes the longest-standing member; the last
/// member leaving deletes the party.
/// </summary>
public class LeavePartyHubCommandHandler : IRequestHandler<LeavePartyHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;
    private readonly ILogger<LeavePartyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="LeavePartyHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    /// <param name="logger">Logger instance.</param>
    public LeavePartyHubCommandHandler(PartyStore parties, ILogger<LeavePartyHubCommandHandler> logger)
    {
        _parties = parties;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        LeavePartyHubCommand request,
        CancellationToken cancellationToken)
    {
        var party = await _parties.GetPartyForCharacterAsync(request.CharacterId, cancellationToken);
        if (party is null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are not in a party" };
        }

        var remaining = party.Members
            .Where(member => member.CharacterId != request.CharacterId)
            .OrderBy(member => member.JoinedAt)
            .ToList();

        if (remaining.Count == 0)
        {
            await _parties.DeleteAsync(party.Id, cancellationToken);
            _logger.LogInformation(
                "Party {PartyId} disbanded: its last member {CharacterIdPrefix} left",
                party.Id,
                request.CharacterId.ToString()[..8]);

            return new PartyCommandResult { Success = true, PartyDisbanded = true, PartyId = party.Id };
        }

        if (party.LeaderCharacterId == request.CharacterId)
        {
            var newLeader = remaining[0].CharacterId;
            await _parties.PromoteLeaderAsync(party.Id, newLeader, cancellationToken);
            _logger.LogInformation(
                "Party {PartyId} leadership passed to {CharacterIdPrefix}",
                party.Id,
                newLeader.ToString()[..8]);
        }

        await _parties.RemoveMemberAsync(request.CharacterId, cancellationToken);

        var updated = await _parties.GetPartyAsync(party.Id, cancellationToken);
        return new PartyCommandResult
        {
            Success = true,
            PartyId = party.Id,
            Party = updated is null ? null : await _parties.ToSnapshotAsync(updated, cancellationToken),
        };
    }
}

/// <summary>Handles <see cref="KickPartyMemberHubCommand"/>; only the leader may remove somebody else.</summary>
public class KickPartyMemberHubCommandHandler : IRequestHandler<KickPartyMemberHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;
    private readonly ILogger<KickPartyMemberHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="KickPartyMemberHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    /// <param name="logger">Logger instance.</param>
    public KickPartyMemberHubCommandHandler(PartyStore parties, ILogger<KickPartyMemberHubCommandHandler> logger)
    {
        _parties = parties;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        KickPartyMemberHubCommand request,
        CancellationToken cancellationToken)
    {
        var party = await _parties.GetPartyForCharacterAsync(request.LeaderCharacterId, cancellationToken);
        if (party is null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are not in a party" };
        }

        if (party.LeaderCharacterId != request.LeaderCharacterId)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "Only the party leader can remove members" };
        }

        if (request.TargetCharacterId == request.LeaderCharacterId)
        {
            return new PartyCommandResult
            {
                Success = false,
                ErrorMessage = "A leader cannot remove themselves; leave the party instead",
            };
        }

        if (party.Members.All(member => member.CharacterId != request.TargetCharacterId))
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "That character is not in your party" };
        }

        await _parties.RemoveMemberAsync(request.TargetCharacterId, cancellationToken);
        _logger.LogInformation(
            "Character {CharacterIdPrefix} removed {TargetIdPrefix} from party {PartyId}",
            request.LeaderCharacterId.ToString()[..8],
            request.TargetCharacterId.ToString()[..8],
            party.Id);

        var updated = await _parties.GetPartyAsync(party.Id, cancellationToken);
        return new PartyCommandResult
        {
            Success = true,
            Party = updated is null ? null : await _parties.ToSnapshotAsync(updated, cancellationToken),
        };
    }
}

/// <summary>
/// Handles <see cref="GetPartyHubQuery"/>. Being solo is a successful answer with no party, not an error — the client
/// calls this on every login.
/// </summary>
public class GetPartyHubQueryHandler : IRequestHandler<GetPartyHubQuery, PartyCommandResult>
{
    private readonly PartyStore _parties;

    /// <summary>Initializes a new instance of <see cref="GetPartyHubQueryHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    public GetPartyHubQueryHandler(PartyStore parties)
    {
        _parties = parties;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(GetPartyHubQuery request, CancellationToken cancellationToken)
    {
        var party = await _parties.GetPartyForCharacterAsync(request.CharacterId, cancellationToken);
        if (party is null)
        {
            return new PartyCommandResult { Success = true };
        }

        return new PartyCommandResult
        {
            Success = true,
            Party = await _parties.ToSnapshotAsync(party, cancellationToken),
        };
    }
}
