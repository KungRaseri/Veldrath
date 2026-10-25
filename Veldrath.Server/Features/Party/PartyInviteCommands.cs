using MediatR;
using Microsoft.Extensions.Options;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Party;

/// <summary>Invites a character, by name, to the caller's party.</summary>
/// <param name="InviterCharacterId">Character sending the invite; must be a member of a party.</param>
/// <param name="TargetCharacterName">Name of the character to invite.</param>
public record InviteToPartyHubCommand(Guid InviterCharacterId, string TargetCharacterName)
    : IRequest<PartyCommandResult>;

/// <summary>Accepts a pending invite and joins the party.</summary>
/// <param name="CharacterId">Invited character accepting.</param>
/// <param name="PartyId">Party being joined.</param>
public record AcceptPartyInviteHubCommand(Guid CharacterId, Guid PartyId) : IRequest<PartyCommandResult>;

/// <summary>Declines a pending invite.</summary>
/// <param name="CharacterId">Invited character declining.</param>
/// <param name="PartyId">Party being declined.</param>
public record DeclinePartyInviteHubCommand(Guid CharacterId, Guid PartyId) : IRequest<PartyCommandResult>;

/// <summary>
/// Handles <see cref="InviteToPartyHubCommand"/>. Every refusal names the reason, because a silent invite is worse
/// than a rejected one: the inviter would sit waiting for a toast that never arrives.
/// </summary>
public class InviteToPartyHubCommandHandler : IRequestHandler<InviteToPartyHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;
    private readonly ICharacterRepository _characters;
    private readonly IPlayerSessionRepository _sessions;
    private readonly ICombatSessionStore _combat;
    private readonly PartyOptions _options;
    private readonly ILogger<InviteToPartyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="InviteToPartyHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    /// <param name="characters">Character repository, used to resolve the invitee by name.</param>
    /// <param name="sessions">Session repository, used to check the invitee is online.</param>
    /// <param name="combat">Combat session store, used to refuse inviting someone mid-fight.</param>
    /// <param name="options">Party tuning (size cap, invite lifetime).</param>
    /// <param name="logger">Logger instance.</param>
    public InviteToPartyHubCommandHandler(
        PartyStore parties,
        ICharacterRepository characters,
        IPlayerSessionRepository sessions,
        ICombatSessionStore combat,
        IOptions<PartyOptions> options,
        ILogger<InviteToPartyHubCommandHandler> logger)
    {
        _parties = parties;
        _characters = characters;
        _sessions = sessions;
        _combat = combat;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        InviteToPartyHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetCharacterName))
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "Name a character to invite" };
        }

        var party = await _parties.GetPartyForCharacterAsync(request.InviterCharacterId, cancellationToken);
        if (party is null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are not in a party" };
        }

        var name = request.TargetCharacterName.Trim();
        var target = await _characters.GetByNameAsync(name, cancellationToken);
        if (target is null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = $"No character named '{name}'" };
        }

        if (target.Id == request.InviterCharacterId)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You cannot invite yourself" };
        }

        if (await _sessions.GetByCharacterIdAsync(target.Id) is null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = $"{target.Name} is not online" };
        }

        if (await _parties.GetPartyForCharacterAsync(target.Id, cancellationToken) is not null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = $"{target.Name} is already in a party" };
        }

        if (await _combat.IsInCombatAsync(target.Id, cancellationToken))
        {
            return new PartyCommandResult { Success = false, ErrorMessage = $"{target.Name} is in combat" };
        }

        var memberCount = await _parties.GetMemberCountAsync(party.Id, cancellationToken);
        if (memberCount >= _options.MaxSize)
        {
            return new PartyCommandResult
            {
                Success = false,
                ErrorMessage = $"Your party is full ({_options.MaxSize} members)",
            };
        }

        await _parties.CreateOrRefreshInviteAsync(
            party.Id,
            target.Id,
            request.InviterCharacterId,
            DateTimeOffset.UtcNow + _options.InviteExpiry,
            cancellationToken);

        _logger.LogInformation(
            "Character {InviterIdPrefix} invited {TargetName} to party {PartyId}",
            request.InviterCharacterId.ToString()[..8],
            target.Name,
            party.Id);

        return new PartyCommandResult
        {
            Success = true,
            PartyId = party.Id,
            InvitedCharacterId = target.Id,
            Party = await _parties.ToSnapshotAsync(party, cancellationToken),
        };
    }
}

/// <summary>
/// Handles <see cref="AcceptPartyInviteHubCommand"/>: validates the invite, joins the party, and clears the invitee's
/// other invitations so they cannot end up in two parties by accepting twice.
/// </summary>
public class AcceptPartyInviteHubCommandHandler : IRequestHandler<AcceptPartyInviteHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;
    private readonly PartyOptions _options;
    private readonly ILogger<AcceptPartyInviteHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="AcceptPartyInviteHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    /// <param name="options">Party tuning (size cap).</param>
    /// <param name="logger">Logger instance.</param>
    public AcceptPartyInviteHubCommandHandler(
        PartyStore parties,
        IOptions<PartyOptions> options,
        ILogger<AcceptPartyInviteHubCommandHandler> logger)
    {
        _parties = parties;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        AcceptPartyInviteHubCommand request,
        CancellationToken cancellationToken)
    {
        var invite = await _parties.GetValidInviteAsync(request.PartyId, request.CharacterId, cancellationToken);
        if (invite is null)
        {
            return new PartyCommandResult
            {
                Success = false,
                ErrorMessage = "That invite has expired or no longer exists",
            };
        }

        if (await _parties.GetPartyForCharacterAsync(request.CharacterId, cancellationToken) is not null)
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are already in a party" };
        }

        var party = await _parties.GetPartyAsync(request.PartyId, cancellationToken);
        if (party is null)
        {
            await _parties.DeleteInvitesForCharacterAsync(request.CharacterId, cancellationToken);
            return new PartyCommandResult { Success = false, ErrorMessage = "That party no longer exists" };
        }

        if (await _parties.GetMemberCountAsync(party.Id, cancellationToken) >= _options.MaxSize)
        {
            return new PartyCommandResult
            {
                Success = false,
                ErrorMessage = $"That party is full ({_options.MaxSize} members)",
            };
        }

        if (!await _parties.AddMemberAsync(party.Id, request.CharacterId, cancellationToken))
        {
            return new PartyCommandResult { Success = false, ErrorMessage = "You are already in a party" };
        }

        await _parties.DeleteInvitesForCharacterAsync(request.CharacterId, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} joined party {PartyId}",
            request.CharacterId.ToString()[..8],
            party.Id);

        var updated = await _parties.GetPartyAsync(party.Id, cancellationToken);
        return new PartyCommandResult
        {
            Success = true,
            PartyId = party.Id,
            Party = updated is null ? null : await _parties.ToSnapshotAsync(updated, cancellationToken),
        };
    }
}

/// <summary>Handles <see cref="DeclinePartyInviteHubCommand"/> by removing just that party's invite.</summary>
public class DeclinePartyInviteHubCommandHandler : IRequestHandler<DeclinePartyInviteHubCommand, PartyCommandResult>
{
    private readonly PartyStore _parties;

    /// <summary>Initializes a new instance of <see cref="DeclinePartyInviteHubCommandHandler"/>.</summary>
    /// <param name="parties">Party persistence helper.</param>
    public DeclinePartyInviteHubCommandHandler(PartyStore parties)
    {
        _parties = parties;
    }

    /// <inheritdoc />
    public async Task<PartyCommandResult> Handle(
        DeclinePartyInviteHubCommand request,
        CancellationToken cancellationToken)
    {
        await _parties.DeleteInviteAsync(request.PartyId, request.CharacterId, cancellationToken);

        return new PartyCommandResult
        {
            Success = true,
            PartyId = request.PartyId,
            PendingInvites = await _parties.GetPendingInvitesAsync(request.CharacterId, cancellationToken),
        };
    }
}
