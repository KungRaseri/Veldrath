using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Hubs;

/// <summary>
/// Real-time game hub. Clients must connect with a valid JWT (passed as query-string
/// <c>access_token</c> parameter, which the JWT middleware picks up from the request).
/// 
/// Flow:
///   1. Client connects (JWT validated automatically).
///   2. Client calls <see cref="SelectCharacter"/> to attach a character.
///   3. Client calls <see cref="EnterZone"/> to join a zone SignalR group.
///   4. All players in the zone receive <c>PlayerEntered</c> / <c>PlayerLeft</c> broadcasts.
/// </summary>
[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly ICharacterRepository _characterRepo;
    private readonly IZoneRepository _zoneRepo;
    private readonly IZoneSessionRepository _zoneSessionRepo;

    public GameHub(
        ILogger<GameHub> logger,
        ICharacterRepository characterRepo,
        IZoneRepository zoneRepo,
        IZoneSessionRepository zoneSessionRepo)
    {
        _logger = logger;
        _characterRepo = characterRepo;
        _zoneRepo = zoneRepo;
        _zoneSessionRepo = zoneSessionRepo;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up zone session — broadcast departure to zone peers
        await LeaveCurrentZoneAsync(Context.ConnectionId, notifyPeers: true);

        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Character selection ───────────────────────────────────────────────────

    /// <summary>
    /// Attach a character to this connection. Must be called before <see cref="EnterZone"/>.
    /// </summary>
    public async Task SelectCharacter(Guid characterId)
    {
        var accountId = GetAccountId();
        var character = await _characterRepo.GetByIdAsync(characterId);

        if (character is null)
        {
            await Clients.Caller.SendAsync("Error", "Character not found");
            return;
        }

        if (character.AccountId != accountId)
        {
            await Clients.Caller.SendAsync("Error", "Character does not belong to this account");
            return;
        }

        Context.Items["CharacterId"]   = character.Id;
        Context.Items["CharacterName"] = character.Name;
        Context.Items["CurrentZoneId"] = character.CurrentZoneId;

        _logger.LogInformation(
            "Character {CharacterName} ({CharacterId}) selected",
            character.Name, character.Id);

        await Clients.Caller.SendAsync("CharacterSelected", new
        {
            character.Id,
            character.Name,
            character.ClassName,
            character.Level,
            character.CurrentZoneId,
            SelectedAt = DateTimeOffset.UtcNow,
        });
    }

    // ── Zone management ───────────────────────────────────────────────────────

    /// <summary>
    /// Join a zone. Broadcasts <c>PlayerEntered</c> to all existing members of the zone,
    /// and sends <c>ZoneState</c> (current occupants) back to the caller.
    /// </summary>
    public async Task EnterZone(string zoneId)
    {
        if (!TryGetCharacterId(out var characterId) || !TryGetCharacterName(out var characterName))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before EnterZone");
            return;
        }

        var zone = await _zoneRepo.GetByIdAsync(zoneId);
        if (zone is null)
        {
            await Clients.Caller.SendAsync("Error", $"Zone '{zoneId}' does not exist");
            return;
        }

        // Leave current zone first (if in one)
        await LeaveCurrentZoneAsync(Context.ConnectionId, notifyPeers: true);

        // Remove any stale session for this character (e.g. unexpected disconnect didn't clean up)
        var stale = await _zoneSessionRepo.GetByCharacterIdAsync(characterId);
        if (stale is not null) await _zoneSessionRepo.RemoveAsync(stale);

        // Create new session
        var session = new ZoneSession
        {
            CharacterId   = characterId,
            CharacterName = characterName,
            ConnectionId  = Context.ConnectionId,
            ZoneId        = zoneId,
        };
        await _zoneSessionRepo.AddAsync(session);

        // Persist last-known zone on the character row
        await _characterRepo.UpdateCurrentZoneAsync(characterId, zoneId);

        Context.Items["CurrentZoneId"] = zoneId;

        // Join SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, ZoneGroup(zoneId));

        // Announce arrival to other players already in zone
        await Clients.OthersInGroup(ZoneGroup(zoneId)).SendAsync("PlayerEntered", new
        {
            CharacterId   = characterId,
            CharacterName = characterName,
            ZoneId        = zoneId,
        });

        // Send current zone occupants back to caller
        var occupants = (await _zoneSessionRepo.GetByZoneIdAsync(zoneId))
            .Select(s => new { s.CharacterId, s.CharacterName, s.EnteredAt });

        await Clients.Caller.SendAsync("ZoneEntered", new
        {
            zone.Id,
            zone.Name,
            zone.Description,
            ZoneType = zone.Type.ToString(),
            Occupants = occupants,
        });

        _logger.LogInformation("Character {Name} entered zone {ZoneId}", characterName, zoneId);
    }

    /// <summary>Voluntarily leave the current zone.</summary>
    public async Task LeaveZone()
    {
        await LeaveCurrentZoneAsync(Context.ConnectionId, notifyPeers: true);
        await Clients.Caller.SendAsync("ZoneLeft");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task LeaveCurrentZoneAsync(string connectionId, bool notifyPeers)
    {
        var session = await _zoneSessionRepo.GetByConnectionIdAsync(connectionId);
        if (session is null) return;

        var zoneId        = session.ZoneId;
        var characterId   = session.CharacterId;
        var characterName = session.CharacterName;

        await _zoneSessionRepo.RemoveAsync(session);
        await Groups.RemoveFromGroupAsync(connectionId, ZoneGroup(zoneId));

        if (notifyPeers)
        {
            await Clients.Group(ZoneGroup(zoneId)).SendAsync("PlayerLeft", new
            {
                CharacterId   = characterId,
                CharacterName = characterName,
                ZoneId        = zoneId,
            });
        }

        _logger.LogInformation("Character {Name} left zone {ZoneId}", characterName, zoneId);
    }

    private static string ZoneGroup(string zoneId) => $"zone:{zoneId}";

    private Guid GetAccountId()
    {
        var value = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from JWT.");
        return Guid.Parse(value);
    }

    private bool TryGetCharacterId(out Guid id)
    {
        if (Context.Items.TryGetValue("CharacterId", out var val) && val is Guid g)
        {
            id = g;
            return true;
        }
        id = default;
        return false;
    }

    private bool TryGetCharacterName(out string name)
    {
        if (Context.Items.TryGetValue("CharacterName", out var val) && val is string s)
        {
            name = s;
            return true;
        }
        name = string.Empty;
        return false;
    }
}

