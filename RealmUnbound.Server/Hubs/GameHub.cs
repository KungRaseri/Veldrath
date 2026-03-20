using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters;
using RealmUnbound.Server.Features.LevelUp;
using RealmUnbound.Server.Services;

namespace RealmUnbound.Server.Hubs;

/// <summary>
/// Real-time game hub. Clients must connect with a valid JWT (passed as query-string
/// <c>access_token</c> parameter, which the JWT middleware picks up from the request).
/// 
/// Flow:
///   1. Client connects (JWT validated automatically). Joins the account-level broadcast group.
///   2. Client calls <see cref="SelectCharacter"/> to attach a character (enforces single active instance).
///   3. Client calls <see cref="EnterZone"/> to join a zone SignalR group.
///   4. All players in the zone receive <c>PlayerEntered</c> / <c>PlayerLeft</c> broadcasts.
///   5. All connections for the same account receive <c>CharacterStatusChanged</c> broadcasts.
/// </summary>
[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly ICharacterRepository _characterRepo;
    private readonly IZoneRepository _zoneRepo;
    private readonly IZoneSessionRepository _zoneSessionRepo;
    private readonly IActiveCharacterTracker _activeCharacters;
    private readonly ISender _mediator;

    /// <summary>Initializes a new instance of <see cref="GameHub"/>.</summary>
    public GameHub(
        ILogger<GameHub> logger,
        ICharacterRepository characterRepo,
        IZoneRepository zoneRepo,
        IZoneSessionRepository zoneSessionRepo,
        IActiveCharacterTracker activeCharacters,
        ISender mediator)
    {
        _logger          = logger;
        _characterRepo   = characterRepo;
        _zoneRepo        = zoneRepo;
        _zoneSessionRepo = zoneSessionRepo;
        _activeCharacters = activeCharacters;
        _mediator        = mediator;
    }

    public override async Task OnConnectedAsync()
    {
        var accountId = GetAccountId();
        Context.Items["AccountId"] = accountId;

        // Join the per-account group so this connection receives CharacterStatusChanged broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));

        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Release any active character claim and notify account peers
        var characterId = _activeCharacters.GetCharacterForConnection(Context.ConnectionId);
        _activeCharacters.Release(Context.ConnectionId);

        if (characterId.HasValue && Context.Items.TryGetValue("AccountId", out var aid) && aid is Guid accountId)
        {
            await Clients.Group(AccountGroup(accountId)).SendAsync("CharacterStatusChanged", new
            {
                CharacterId = characterId.Value,
                IsOnline = false,
            });
        }

        // Clean up zone session — broadcast departure to zone peers
        await LeaveCurrentZoneAsync(Context.ConnectionId, notifyPeers: true);

        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ── Character selection ───────────────────────────────────────────────────

    /// <summary>
    /// Attach a character to this connection. Must be called before <see cref="EnterZone"/>.
    /// Only one active connection may hold a given character at a time; attempting to claim
    /// an already-active character sends a <c>CharacterAlreadyActive</c> message and returns.
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

        // Enforce single active instance per character across all connections
        if (!_activeCharacters.TryClaim(characterId, Context.ConnectionId))
        {
            await Clients.Caller.SendAsync("CharacterAlreadyActive", characterId);
            _logger.LogWarning(
                "Character {CharacterName} ({CharacterId}) is already active; rejected duplicate claim from {ConnectionId}",
                character.Name, characterId, Context.ConnectionId);
            return;
        }

        Context.Items["CharacterId"]   = character.Id;
        Context.Items["CharacterName"] = character.Name;
        Context.Items["CurrentZoneId"] = character.CurrentZoneId;

        _logger.LogInformation(
            "Character {CharacterName} ({CharacterId}) selected by {ConnectionId}",
            character.Name, character.Id, Context.ConnectionId);

        // Broadcast to all connections in this account's group (including other open clients)
        await Clients.Group(AccountGroup(accountId)).SendAsync("CharacterStatusChanged", new
        {
            CharacterId = characterId,
            IsOnline = true,
        });

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

    /// <summary>
    /// Returns the set of character IDs that are currently active (i.e. claimed by any connection).
    /// Used by the character select screen to show which characters are already in use.
    /// </summary>
    public Task<IEnumerable<Guid>> GetActiveCharacters() =>
        Task.FromResult(_activeCharacters.GetActiveCharacterIds().AsEnumerable());

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

        // Remove only genuinely stale sessions for this character (from a previous disconnect that
        // didn't clean up). If the session belongs to a different live connection, SelectCharacter
        // would have already rejected this attempt via IActiveCharacterTracker.
        var stale = await _zoneSessionRepo.GetByCharacterIdAsync(characterId);
        if (stale is not null && stale.ConnectionId != Context.ConnectionId)
            await _zoneSessionRepo.RemoveAsync(stale);

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

    // ── Game actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Award experience points to the caller's active character.
    /// Validates character ownership via <see cref="IActiveCharacterTracker"/> before dispatching
    /// the command to the MediatR pipeline. Broadcasts the outcome to the character's current
    /// zone group; falls back to the caller only when the character is not in a zone.
    /// </summary>
    /// <param name="amount">Positive number of experience points to award.</param>
    /// <param name="source">Optional label for the XP source (e.g. <c>"Combat"</c>, <c>"Quest"</c>).</param>
    public async Task GainExperience(int amount, string? source = null)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GainExperience");
            return;
        }

        try
        {
            var result = await _mediator.Send(new GainExperienceHubCommand
            {
                CharacterId = characterId,
                Amount      = amount,
                Source      = source,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to gain experience");
                return;
            }

            var payload = new
            {
                CharacterId   = characterId,
                result.NewLevel,
                result.NewExperience,
                result.LeveledUp,
                result.LeveledUpTo,
                Source        = source,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("ExperienceGained", payload);
            else
                await Clients.Caller.SendAsync("ExperienceGained", payload);

            _logger.LogInformation(
                "Character {CharacterId} gained {Amount} XP from {Source}; now level {Level}",
                characterId, amount, source ?? "Unknown", result.NewLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GainExperience for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to process experience gain");
        }
    }

    // ── Character progression ──────────────────────────────────────────────

    /// <summary>
    /// Spend a character's unallocated attribute points.
    /// Validates character ownership via <see cref="IActiveCharacterTracker"/> before dispatching
    /// to the MediatR pipeline. Sends <c>AttributePointsAllocated</c> to the zone group
    /// (or the caller only when not in a zone) on success.
    /// </summary>
    /// <param name="allocations">
    /// Map of attribute name (e.g. <c>"Strength"</c>) to the number of points to spend.
    /// All values must be positive integers.
    /// </param>
    public async Task AllocateAttributePoints(Dictionary<string, int> allocations)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before AllocateAttributePoints");
            return;
        }

        try
        {
            var result = await _mediator.Send(new AllocateAttributePointsHubCommand
            {
                CharacterId = characterId,
                Allocations = allocations,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to allocate attribute points");
                return;
            }

            var payload = new
            {
                CharacterId     = characterId,
                result.PointsSpent,
                result.RemainingPoints,
                result.NewAttributes,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("AttributePointsAllocated", payload);
            else
                await Clients.Caller.SendAsync("AttributePointsAllocated", payload);

            _logger.LogInformation(
                "Character {CharacterId} allocated {Points} attribute points; {Remaining} remaining",
                characterId, result.PointsSpent, result.RemainingPoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AllocateAttributePoints for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to allocate attribute points");
        }
    }

    // ── Rest / Recovery ───────────────────────────────────────────────────────

    /// <summary>
    /// Rest at an inn or rest point, restoring the caller's active character to full health
    /// and mana in exchange for a gold cost stored in the character's attributes blob.
    /// Broadcasts <c>CharacterRested</c> to the zone group (or the caller only when not in a zone)
    /// on success, and sends <c>Error</c> on validation failure or handler error.
    /// </summary>
    /// <param name="locationId">ID of the inn or rest-point location.</param>
    /// <param name="costInGold">Gold deducted for the rest (default: 10).</param>
    public async Task RestAtLocation(string locationId, int costInGold = 10)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before RestAtLocation");
            return;
        }

        try
        {
            var result = await _mediator.Send(new RestAtLocationHubCommand
            {
                CharacterId = characterId,
                LocationId  = locationId,
                CostInGold  = costInGold,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to rest at location");
                return;
            }

            var payload = new
            {
                CharacterId    = characterId,
                LocationId     = locationId,
                result.CurrentHealth,
                result.MaxHealth,
                result.CurrentMana,
                result.MaxMana,
                result.GoldRemaining,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("CharacterRested", payload);
            else
                await Clients.Caller.SendAsync("CharacterRested", payload);

            _logger.LogInformation(
                "Character {CharacterId} rested at {LocationId}; HP {Hp}/{MaxHp}, MP {Mp}/{MaxMp}, gold remaining {Gold}",
                characterId, locationId, result.CurrentHealth, result.MaxHealth,
                result.CurrentMana, result.MaxMana, result.GoldRemaining);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RestAtLocation for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to process rest at location");
        }
    }

    /// <summary>
    /// Activate an ability for the caller's active character, consuming mana and optionally
    /// restoring health for healing abilities.
    /// Validates character ownership via <see cref="IActiveCharacterTracker"/> before dispatching
    /// to the MediatR pipeline. Sends <c>AbilityUsed</c> to the zone group
    /// (or the caller only when not in a zone) on success.
    /// </summary>
    /// <param name="abilityId">
    /// ID of the ability to activate (e.g. <c>"fireball"</c>, <c>"heal"</c>).
    /// Ability IDs that contain <c>"heal"</c> (case-insensitive) also restore hit points.
    /// </param>
    public async Task UseAbility(string abilityId)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before UseAbility");
            return;
        }

        try
        {
            var result = await _mediator.Send(new UseAbilityHubCommand
            {
                CharacterId = characterId,
                AbilityId   = abilityId,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to use ability");
                return;
            }

            var payload = new
            {
                CharacterId    = characterId,
                result.AbilityId,
                result.ManaCost,
                result.RemainingMana,
                result.HealthRestored,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("AbilityUsed", payload);
            else
                await Clients.Caller.SendAsync("AbilityUsed", payload);

            _logger.LogInformation(
                "Character {CharacterId} used ability {AbilityId}; {Mana} mana remaining, {Heal} HP restored",
                characterId, abilityId, result.RemainingMana, result.HealthRestored);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UseAbility for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to use ability");
        }
    }

    /// <summary>
    /// Awards skill XP to the caller's active character for the specified skill.
    /// Broadcasts <c>SkillXpGained</c> to the zone group (or back to the caller when not in a zone).
    /// </summary>
    /// <param name="request">Skill identifier and XP amount to award.</param>
    public async Task AwardSkillXp(AwardSkillXpHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before AwardSkillXp");
            return;
        }

        try
        {
            var result = await _mediator.Send(new AwardSkillXpHubCommand
            {
                CharacterId = characterId,
                SkillId     = request.SkillId,
                Amount      = request.Amount,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to award skill XP");
                return;
            }

            var payload = new
            {
                CharacterId  = characterId,
                result.SkillId,
                result.TotalXp,
                result.CurrentRank,
                result.RankedUp,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("SkillXpGained", payload);
            else
                await Clients.Caller.SendAsync("SkillXpGained", payload);

            _logger.LogInformation(
                "Character {CharacterId} earned {Amount} XP in skill {SkillId}; total {Total}, rank {Rank}",
                characterId, request.Amount, request.SkillId, result.TotalXp, result.CurrentRank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AwardSkillXp for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to award skill XP");
        }
    }

    /// <summary>
    /// Equips or unequips an item in a named slot for the caller's active character.
    /// Broadcasts <c>ItemEquipped</c> to the zone group (or back to the caller when not in a zone).
    /// Pass <see langword="null"/> as <paramref name="itemRef"/> to clear the slot.
    /// </summary>
    /// <param name="request">Slot name and item-reference slug (or null to unequip).</param>
    public async Task EquipItem(EquipItemHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before EquipItem");
            return;
        }

        try
        {
            var result = await _mediator.Send(new EquipItemHubCommand
            {
                CharacterId = characterId,
                Slot        = request.Slot,
                ItemRef     = request.ItemRef,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to equip item");
                return;
            }

            var payload = new
            {
                CharacterId      = characterId,
                result.Slot,
                result.ItemRef,
                result.AllEquippedItems,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("ItemEquipped", payload);
            else
                await Clients.Caller.SendAsync("ItemEquipped", payload);

            _logger.LogInformation(
                "Character {CharacterId} {Action} '{ItemRef}' in slot {Slot}",
                characterId,
                request.ItemRef is null ? "cleared" : "equipped",
                request.ItemRef ?? "(none)",
                result.Slot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EquipItem for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to equip item");
        }
    }

    /// <summary>
    /// Add or remove gold from the caller's active character.
    /// Broadcasts <c>GoldChanged</c> to the zone group (or back to the caller when not in a zone).
    /// Pass a negative <paramref name="amount"/> to spend gold (e.g. making a purchase).
    /// </summary>
    /// <param name="amount">Gold to add (positive) or spend (negative). Cannot be zero.</param>
    /// <param name="source">Optional label for the source or sink (e.g. <c>"Loot"</c>, <c>"Quest"</c>).</param>
    public async Task AddGold(int amount, string? source = null)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before AddGold");
            return;
        }

        try
        {
            var result = await _mediator.Send(new AddGoldHubCommand
            {
                CharacterId = characterId,
                Amount      = amount,
                Source      = source,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to modify gold");
                return;
            }

            var payload = new
            {
                CharacterId  = characterId,
                result.GoldAdded,
                result.NewGoldTotal,
                Source       = source,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("GoldChanged", payload);
            else
                await Clients.Caller.SendAsync("GoldChanged", payload);

            _logger.LogInformation(
                "Character {CharacterId} gold changed by {Amount} ({Source}); total now {Total}",
                characterId, amount, source ?? "Unknown", result.NewGoldTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AddGold for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to modify gold");
        }
    }

    /// <summary>
    /// Apply damage to the caller's active character, reducing current health (clamped to zero).
    /// Broadcasts <c>DamageTaken</c> to the zone group (or back to the caller when not in a zone).
    /// </summary>
    /// <param name="damageAmount">Positive number of hit points to remove.</param>
    /// <param name="source">Optional label for the damage source (e.g. <c>"Enemy"</c>, <c>"Trap"</c>).</param>
    public async Task TakeDamage(int damageAmount, string? source = null)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before TakeDamage");
            return;
        }

        try
        {
            var result = await _mediator.Send(new TakeDamageHubCommand
            {
                CharacterId  = characterId,
                DamageAmount = damageAmount,
                Source       = source,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to apply damage");
                return;
            }

            var payload = new
            {
                CharacterId   = characterId,
                DamageAmount  = damageAmount,
                result.CurrentHealth,
                result.MaxHealth,
                result.IsDead,
                Source        = source,
            };

            if (Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zoneId && !string.IsNullOrEmpty(zoneId))
                await Clients.Group(ZoneGroup(zoneId)).SendAsync("DamageTaken", payload);
            else
                await Clients.Caller.SendAsync("DamageTaken", payload);

            _logger.LogInformation(
                "Character {CharacterId} took {Damage} damage from {Source}; HP {Hp}/{Max} IsDead={Dead}",
                characterId, damageAmount, source ?? "Unknown", result.CurrentHealth, result.MaxHealth, result.IsDead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TakeDamage for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to apply damage");
        }
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
    private static string AccountGroup(Guid accountId) => $"account:{accountId}";

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

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.AwardSkillXp"/>.</summary>
/// <param name="SkillId">Skill identifier (e.g. <c>"swordsmanship"</c>, <c>"herbalism"</c>).</param>
/// <param name="Amount">XP amount to award. Must be positive.</param>
public record AwardSkillXpHubRequest(string SkillId, int Amount);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.EquipItem"/>.</summary>
/// <param name="Slot">Slot name (e.g. <c>"MainHand"</c>, <c>"Head"</c>). Must be a known slot.</param>
/// <param name="ItemRef">Item-reference slug to equip, or <see langword="null"/> to clear the slot.</param>
public record EquipItemHubRequest(string Slot, string? ItemRef);

