using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Connection;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters;
using RealmUnbound.Server.Features.Characters.Combat;
using RealmUnbound.Server.Features.LevelUp;
using RealmUnbound.Server.Features.Quest;
using RealmUnbound.Server.Features.Shop;
using RealmUnbound.Server.Features.Zones;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Settings;

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
    private readonly IPlayerSessionRepository _playerSessionRepo;
    private readonly IActiveCharacterTracker _activeCharacters;
    private readonly ISender _mediator;
    private readonly IZoneEntityTracker _entityTracker;
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IEnemyRepository _enemyRepo;
    private readonly IOptions<VersionCompatibilitySettings> _versionOptions;

    /// <summary>Initializes a new instance of <see cref="GameHub"/>.</summary>
    public GameHub(
        ILogger<GameHub> logger,
        ICharacterRepository characterRepo,
        IZoneRepository zoneRepo,
        IPlayerSessionRepository playerSessionRepo,
        IActiveCharacterTracker activeCharacters,
        ISender mediator,
        IZoneEntityTracker entityTracker,
        ITileMapRepository tilemapRepo,
        IEnemyRepository enemyRepo,
        IOptions<VersionCompatibilitySettings> versionOptions)
    {
        _logger           = logger;
        _characterRepo    = characterRepo;
        _zoneRepo           = zoneRepo;
        _playerSessionRepo  = playerSessionRepo;
        _activeCharacters   = activeCharacters;
        _mediator         = mediator;
        _entityTracker    = entityTracker;
        _tilemapRepo      = tilemapRepo;
        _enemyRepo        = enemyRepo;
        _versionOptions   = versionOptions;
    }

    public override async Task OnConnectedAsync()
    {
        var accountId = GetAccountId();
        Context.Items["AccountId"] = accountId;

        // Join the per-account group so this connection receives CharacterStatusChanged broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId));

        // Resolve server version from the assembly (Major.Minor only — patch carries no protocol meaning)
        var v = GetType().Assembly.GetName().Version ?? new Version(0, 1);
        var serverVersion = $"{v.Major}.{v.Minor}";
        var minCompatible = _versionOptions.Value.MinCompatibleClientVersion;

        _logger.LogInformation("Client connected: {ConnectionId} (server v{ServerVersion}, minClient v{MinCompat})",
            Context.ConnectionId, serverVersion, minCompatible);

        await Clients.Caller.SendAsync("ServerInfo",
            new ServerInfoPayload(Context.ConnectionId, serverVersion, minCompatible));

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

    // Character selection
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
        Context.Items["DifficultyMode"] = character.DifficultyMode;

        // Start the character on the region map — derive region from last known zone or fall back to "thornveil"
        var zone     = string.IsNullOrEmpty(character.CurrentZoneId) ? null : await _zoneRepo.GetByIdAsync(character.CurrentZoneId);
        var regionId = zone?.RegionId ?? "thornveil";
        Context.Items["CurrentRegionId"] = regionId;

        // Remove any stale session left by an earlier disconnect that did not clean up
        var staleRegionSession = await _playerSessionRepo.GetByCharacterIdAsync(character.Id);
        if (staleRegionSession is not null)
            await _playerSessionRepo.RemoveAsync(staleRegionSession);

        // Create a region-map session (ZoneId = null means on the region map, not inside a zone)
        await _playerSessionRepo.AddAsync(new PlayerSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = Context.ConnectionId,
            RegionId      = regionId,
            ZoneId        = null,
            TileX         = 1,
            TileY         = 1,
        });

        // Join the per-region broadcast group for RegionPlayerMoved broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, RegionGroup(regionId));

        _logger.LogInformation(
            "Character {CharacterName} ({CharacterId}) selected by {ConnectionId}",
            character.Name, character.Id, Context.ConnectionId);

        // Broadcast to all connections in this account's group (including other open clients)
        await Clients.Group(AccountGroup(accountId)).SendAsync("CharacterStatusChanged", new
        {
            CharacterId = characterId,
            IsOnline = true,
        });

        // Deserialise the attributes blob so the client can seed the HUD immediately
        var initAttrs     = JsonSerializer.Deserialize<Dictionary<string, int>>(
            string.IsNullOrWhiteSpace(character.Attributes) ? "{}" : character.Attributes)
            ?? new Dictionary<string, int>();
        var initMaxHealth = initAttrs.GetValueOrDefault("MaxHealth", character.Level * 10);
        var initMaxMana   = initAttrs.GetValueOrDefault("MaxMana",   character.Level * 5);

        // Deserialise learned ability slugs so the client can render ability buttons immediately.
        var learnedAbilities = JsonSerializer.Deserialize<List<string>>(
            string.IsNullOrWhiteSpace(character.AbilitiesBlob) ? "[]" : character.AbilitiesBlob)
            ?? [];

        await Clients.Caller.SendAsync("CharacterSelected", new
        {
            character.Id,
            character.Name,
            character.ClassName,
            character.Level,
            character.Experience,
            character.CurrentZoneId,
            RegionId               = regionId,
            CurrentHealth          = initAttrs.GetValueOrDefault("CurrentHealth", initMaxHealth),
            MaxHealth              = initMaxHealth,
            CurrentMana            = initAttrs.GetValueOrDefault("CurrentMana", initMaxMana),
            MaxMana                = initMaxMana,
            Gold                   = initAttrs.GetValueOrDefault("Gold", 0),
            UnspentAttributePoints = initAttrs.GetValueOrDefault("UnspentAttributePoints", 0),
            Strength               = initAttrs.GetValueOrDefault("Strength", 10),
            Dexterity              = initAttrs.GetValueOrDefault("Dexterity", 10),
            Constitution           = initAttrs.GetValueOrDefault("Constitution", 10),
            Intelligence           = initAttrs.GetValueOrDefault("Intelligence", 10),
            Wisdom                 = initAttrs.GetValueOrDefault("Wisdom", 10),
            Charisma               = initAttrs.GetValueOrDefault("Charisma", 10),
            LearnedAbilities       = learnedAbilities,
            SelectedAt             = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// Returns the set of character IDs that are currently active (i.e. claimed by any connection).
    /// Used by the character select screen to show which characters are already in use.
    /// </summary>
    public Task<IEnumerable<Guid>> GetActiveCharacters() =>
        Task.FromResult(_activeCharacters.GetActiveCharacterIds().AsEnumerable());

    // Zone management
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
        var stale = await _playerSessionRepo.GetByCharacterIdAsync(characterId);
        if (stale is not null && stale.ConnectionId != Context.ConnectionId)
            await _playerSessionRepo.RemoveAsync(stale);

        // Create new session
        var session = new PlayerSession
        {
            CharacterId   = characterId,
            CharacterName = characterName,
            ConnectionId  = Context.ConnectionId,
            RegionId      = zone.RegionId,
            ZoneId        = zoneId,
        };
        await _playerSessionRepo.AddAsync(session);

        // Persist last-known zone on the character row
        await _characterRepo.UpdateCurrentZoneAsync(characterId, zoneId);

        Context.Items["CurrentZoneId"] = zoneId;

        // Compute and store the zone group name based on zone type and difficulty
        var difficultyMode = Context.Items.TryGetValue("DifficultyMode", out var dm) && dm is string d ? d : "normal";
        var zoneGroup = ComputeZoneGroup(zone.Type, zoneId, difficultyMode);
        Context.Items["CurrentZoneGroupName"] = zoneGroup;

        // Register zone group name for background services (e.g. EnemyAiService)
        _entityTracker.SetZoneGroupName(zoneId, zoneGroup);

        // Join SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, zoneGroup);

        // Announce arrival to other players already in zone
        await Clients.OthersInGroup(zoneGroup).SendAsync("PlayerEntered", new
        {
            CharacterId   = characterId,
            CharacterName = characterName,
            ZoneId        = zoneId,
        });

        // Send current zone occupants back to caller
        var occupants = (await _playerSessionRepo.GetByZoneIdAsync(zoneId))
            .Select(s => new { s.CharacterId, s.CharacterName, s.EnteredAt });

        await Clients.Caller.SendAsync("ZoneEntered", new
        {
            zone.Id,
            zone.Name,
            zone.Description,
            ZoneType = zone.Type.ToString(),
            Occupants = occupants,
        });

        // Determine spawn position (first spawn point or tile 1,1 fallback)
        var map = await _tilemapRepo.GetByZoneIdAsync(zoneId);
        var spawns = map?.GetSpawnPoints();
        var spawnX = spawns is { Count: > 0 } ? spawns[0].TileX : 1;
        var spawnY = spawns is { Count: > 0 } ? spawns[0].TileY : 1;
        _entityTracker.TrackPlayer(zoneId, characterId, spawnX, spawnY);

        // Persist the spawn tile so MoveCharacter can validate 1-tile steps from the correct origin.
        await _characterRepo.UpdateTilePositionAsync(characterId, spawnX, spawnY, zoneId);

        // Spawn enemies only in non-town zones; towns are safe areas with no hostile spawns.
        var existingEntities = _entityTracker.GetEntities(zoneId);
        if (existingEntities.Count == 0 && map is not null && zone.Type != ZoneType.Town)
        {
            var spawned = await SpawnEnemiesForZoneAsync(zoneId, map);
            if (spawned.Count > 0)
            {
                var spawnPayload = new ZoneEntitiesSnapshotPayload(
                    spawned.Select(e => new TileEntityDto(e.EntityId, e.EntityType, e.SpriteKey, e.TileX, e.TileY, e.Direction)).ToList());
                await Clients.OthersInGroup(zoneGroup).SendAsync("ZoneEntitiesSnapshot", spawnPayload);
            }
        }

        // Always send the entering player a complete snapshot: all current enemies plus all tracked
        // player positions. This ensures their own character appears on the tilemap immediately,
        // without waiting for the first CharacterMoved broadcast.
        var allEnemies = _entityTracker.GetEntities(zoneId);
        var allPlayers = _entityTracker.GetPlayerPositions(zoneId);
        var callerSnapshot = new ZoneEntitiesSnapshotPayload(
            allEnemies.Select(e => new TileEntityDto(e.EntityId, e.EntityType, e.SpriteKey, e.TileX, e.TileY, e.Direction))
                      .Concat(allPlayers.Select(p => new TileEntityDto(p.CharacterId, "player", "player", p.X, p.Y, "S")))
                      .ToList());
        await Clients.Caller.SendAsync("ZoneEntitiesSnapshot", callerSnapshot);

        _logger.LogInformation("Character {Name} entered zone {ZoneId}", characterName, zoneId);
    }

    /// <summary>Voluntarily leave the current zone.</summary>
    public async Task LeaveZone()
    {
        await LeaveCurrentZoneAsync(Context.ConnectionId, notifyPeers: true);
        await Clients.Caller.SendAsync("ZoneLeft");
    }

    // Tilemap movement
    /// <summary>
    /// Requests a one-tile movement for the caller's active character.
    /// The server validates the step (1-tile distance, collision mask, 100 ms cooldown) and,
    /// on success, persists the new position and broadcasts <c>CharacterMoved</c> to the zone group.
    /// If the destination is an exit tile the caller additionally receives <c>TileExitTriggered</c>.
    /// </summary>
    /// <param name="request">Target tile coordinates and facing direction.</param>
    public async Task MoveCharacter(MoveCharacterHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before MoveCharacter");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;
        if (string.IsNullOrEmpty(zoneId))
        {
            await Clients.Caller.SendAsync("Error", "EnterZone must be called before MoveCharacter");
            return;
        }

        try
        {
            var result = await _mediator.Send(new MoveCharacterHubCommand(
                characterId, request.ToX, request.ToY, request.Direction, zoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Move rejected");
                return;
            }

            var payload = new CharacterMovedPayload(characterId, result.TileX, result.TileY, result.Direction);

            // Keep player position up-to-date for AI pathfinding
            _entityTracker.TrackPlayer(zoneId, characterId, result.TileX, result.TileY);

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("CharacterMoved", payload);
            else
                await Clients.Caller.SendAsync("CharacterMoved", payload);

            if (result.ExitTriggered is not null)
                await Clients.Caller.SendAsync("TileExitTriggered", result.ExitTriggered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MoveCharacter for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to process move");
        }
    }

    /// <summary>
    /// Loads and returns the <see cref="RealmUnbound.Contracts.Tilemap.TileMapDto"/> for the caller's current zone.
    /// Sends <c>ZoneTileMap</c> to the caller on success.
    /// </summary>
    public async Task GetZoneTileMap()
    {
        if (!TryGetCharacterId(out _))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GetZoneTileMap");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;
        if (string.IsNullOrEmpty(zoneId))
        {
            await Clients.Caller.SendAsync("Error", "EnterZone must be called before GetZoneTileMap");
            return;
        }

        try
        {
            var result = await _mediator.Send(new GetZoneTileMapHubCommand(zoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to load tilemap");
                return;
            }

            await Clients.Caller.SendAsync("ZoneTileMap", result.TileMap);

            _logger.LogDebug("Sent tilemap for zone {ZoneId} to {ConnectionId}", zoneId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetZoneTileMap for zone {ZoneId}", zoneId);
            await Clients.Caller.SendAsync("Error", "Failed to load tilemap");
        }
    }

    // Region map
    /// <summary>
    /// Loads and returns the <see cref="RealmUnbound.Contracts.Tilemap.RegionMapDto"/> for the
    /// caller's current region. Sends <c>RegionMapData</c> to the caller on success.
    /// </summary>
    public async Task GetRegionMap()
    {
        if (!TryGetCharacterId(out _))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GetRegionMap");
            return;
        }

        var regionId = Context.Items.TryGetValue("CurrentRegionId", out var r) && r is string rs ? rs : string.Empty;
        if (string.IsNullOrEmpty(regionId))
        {
            await Clients.Caller.SendAsync("Error", "No active region. Call SelectCharacter first");
            return;
        }

        try
        {
            var result = await _mediator.Send(new GetRegionMapHubCommand(regionId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to load region map");
                return;
            }

            await Clients.Caller.SendAsync("RegionMapData", result.RegionMap);

            _logger.LogDebug("Sent region map for '{RegionId}' to {ConnectionId}", regionId, Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRegionMap for region {RegionId}", regionId);
            await Clients.Caller.SendAsync("Error", "Failed to load region map");
        }
    }

    /// <summary>
    /// Requests a one-tile movement for the caller's active character on the region map.
    /// The server validates the step (1-tile distance, collision mask, 100 ms cooldown) and,
    /// on success, persists the new position and broadcasts <c>RegionPlayerMoved</c> to the region group.
    /// If the destination is a zone-entry tile the caller additionally receives <c>ZoneEntryTriggered</c>;
    /// if it is a region-exit tile the caller receives <c>RegionExitTriggered</c>.
    /// </summary>
    /// <param name="request">Target tile coordinates and facing direction.</param>
    public async Task MoveOnRegion(MoveOnRegionHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before MoveOnRegion");
            return;
        }

        var regionId = Context.Items.TryGetValue("CurrentRegionId", out var r) && r is string rs ? rs : string.Empty;
        if (string.IsNullOrEmpty(regionId))
        {
            await Clients.Caller.SendAsync("Error", "Not on a region map");
            return;
        }

        try
        {
            var result = await _mediator.Send(new MoveOnRegionHubCommand(
                characterId, request.ToX, request.ToY, request.Direction, regionId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Move rejected");
                return;
            }

            var payload = new RegionPlayerMovedPayload(characterId, result.TileX, result.TileY, result.Direction);
            await Clients.Group(RegionGroup(regionId)).SendAsync("RegionPlayerMoved", payload);

            if (result.ZoneEntryTriggered is not null)
                await Clients.Caller.SendAsync("ZoneEntryTriggered", result.ZoneEntryTriggered);
            else if (result.RegionExitTriggered is not null)
                await Clients.Caller.SendAsync("RegionExitTriggered", result.RegionExitTriggered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MoveOnRegion for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to process region move");
        }
    }

    /// <summary>
    /// Exits the caller's current zone and returns them to the region map at the zone-entry tile.
    /// Leaves the zone SignalR group, joins the region group, and notifies zone peers of the departure.
    /// Sends <c>ZoneExited</c> to the caller with the region ID and spawn tile coordinates.
    /// </summary>
    public async Task ExitZone()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before ExitZone");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string zs ? zs : string.Empty;
        if (string.IsNullOrEmpty(zoneId))
        {
            await Clients.Caller.SendAsync("Error", "Not currently in a zone");
            return;
        }

        var regionId = Context.Items.TryGetValue("CurrentRegionId", out var r) && r is string rs ? rs : "thornveil";

        try
        {
            var result = await _mediator.Send(new ExitZoneHubCommand(characterId, regionId, zoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to exit zone");
                return;
            }

            // Notify zone peers and leave zone SignalR group
            if (TryGetCurrentZoneGroup(out var zoneGroup))
            {
                TryGetCharacterName(out var characterName);
                await Clients.OthersInGroup(zoneGroup).SendAsync("PlayerLeft", new
                {
                    CharacterId   = characterId,
                    CharacterName = characterName,
                    ZoneId        = zoneId,
                });
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, zoneGroup);
            }

            // Clean up entity tracking for the zone we just left
            _entityTracker.UntrackPlayer(zoneId, characterId);
            if (_entityTracker.GetPlayerPositions(zoneId).Count == 0)
                _entityTracker.ClearZone(zoneId);

            // Clear zone context items so subsequent hub calls see the region-map state
            Context.Items["CurrentZoneId"]        = null;
            Context.Items["CurrentZoneGroupName"] = null;

            // Join the region broadcast group
            await Groups.AddToGroupAsync(Context.ConnectionId, RegionGroup(regionId));

            await Clients.Caller.SendAsync("ZoneExited", new
            {
                RegionId   = regionId,
                result.TileX,
                result.TileY,
            });

            _logger.LogInformation(
                "Character {CharacterId} exited zone '{ZoneId}' → region '{RegionId}' at ({X},{Y})",
                characterId, zoneId, regionId, result.TileX, result.TileY);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExitZone for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to exit zone");
        }
    }

    /// <summary>
    /// Award experience points to the caller's active character.
    /// Validates character ownership via <see cref="IActiveCharacterTracker"/> before dispatching
    /// the command to the MediatR pipeline. Broadcasts the outcome to the character's current
    /// zone group; falls back to the caller only when the character is not in a zone.
    /// </summary>
    /// <param name="amount">Positive number of experience points to award.</param>
    /// <param name="source">Optional label for the XP source (e.g. <c>"Combat"</c>, <c>"Quest"</c>).</param>
    public async Task GainExperience(GainExperienceHubRequest request)
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
                Amount      = request.Amount,
                Source      = request.Source,
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
                Source        = request.Source,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("ExperienceGained", payload);
            else
                await Clients.Caller.SendAsync("ExperienceGained", payload);

            _logger.LogInformation(
                "Character {CharacterId} gained {Amount} XP from {Source}; now level {Level}",
                characterId, request.Amount, request.Source ?? "Unknown", result.NewLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GainExperience for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to process experience gain");
        }
    }

    // Character progression
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

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("AttributePointsAllocated", payload);
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

    // Rest / Recovery
    /// <summary>
    /// Rest at an inn or rest point, restoring the caller's active character to full health
    /// and mana in exchange for a gold cost stored in the character's attributes blob.
    /// Broadcasts <c>CharacterRested</c> to the zone group (or the caller only when not in a zone)
    /// on success, and sends <c>Error</c> on validation failure or handler error.
    /// </summary>
    /// <param name="request">Request DTO containing the location ID and optional gold cost.</param>
    public async Task RestAtLocation(RestAtLocationHubRequest request)
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
                LocationId  = request.LocationId,
                CostInGold  = request.CostInGold,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to rest at location");
                return;
            }

            var payload = new
            {
                CharacterId    = characterId,
                LocationId     = request.LocationId,
                result.CurrentHealth,
                result.MaxHealth,
                result.CurrentMana,
                result.MaxMana,
                result.GoldRemaining,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("CharacterRested", payload);
            else
                await Clients.Caller.SendAsync("CharacterRested", payload);

            _logger.LogInformation(
                "Character {CharacterId} rested at {LocationId}; HP {Hp}/{MaxHp}, MP {Mp}/{MaxMp}, gold remaining {Gold}",
                characterId, request.LocationId, result.CurrentHealth, result.MaxHealth,
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

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("AbilityUsed", payload);
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

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("SkillXpGained", payload);
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

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("ItemEquipped", payload);
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
    public async Task AddGold(AddGoldHubRequest request)
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
                Amount      = request.Amount,
                Source      = request.Source,
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
                Source       = request.Source,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("GoldChanged", payload);
            else
                await Clients.Caller.SendAsync("GoldChanged", payload);

            _logger.LogInformation(
                "Character {CharacterId} gold changed by {Amount} ({Source}); total now {Total}",
                characterId, request.Amount, request.Source ?? "Unknown", result.NewGoldTotal);
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
    public async Task TakeDamage(TakeDamageHubRequest request)
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
                DamageAmount = request.DamageAmount,
                Source       = request.Source,
            });

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to apply damage");
                return;
            }

            var payload = new
            {
                CharacterId   = characterId,
                DamageAmount  = request.DamageAmount,
                result.CurrentHealth,
                result.MaxHealth,
                result.IsDead,
                Source        = request.Source,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("DamageTaken", payload);
            else
                await Clients.Caller.SendAsync("DamageTaken", payload);

            _logger.LogInformation(
                "Character {CharacterId} took {Damage} damage from {Source}; HP {Hp}/{Max} IsDead={Dead}",
                characterId, request.DamageAmount, request.Source ?? "Unknown", result.CurrentHealth, result.MaxHealth, result.IsDead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TakeDamage for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to apply damage");
        }
    }

    /// <summary>
    /// Craft an item using the named recipe, deducting the crafting cost from the caller's
    /// active character's gold. Broadcasts <c>ItemCrafted</c> to the zone group (or back to
    /// the caller when not in a zone).
    /// </summary>
    /// <param name="recipeSlug">The slug of the recipe to craft (e.g. <c>"iron-sword"</c>).</param>
    public async Task CraftItem(string recipeSlug)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before CraftItem");
            return;
        }

        try
        {
            var result = await _mediator.Send(new CraftItemHubCommand(characterId, recipeSlug));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to craft item");
                return;
            }

            var payload = new
            {
                CharacterId   = characterId,
                RecipeSlug    = recipeSlug,
                result.GoldSpent,
                result.RemainingGold,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("ItemCrafted", payload);
            else
                await Clients.Caller.SendAsync("ItemCrafted", payload);

            _logger.LogInformation(
                "Character {CharacterId} crafted '{RecipeSlug}'; remaining gold {Gold}",
                characterId, recipeSlug, result.RemainingGold);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CraftItem for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to craft item");
        }
    }

    /// <summary>
    /// Enter a dungeon zone by slug, looking up the dungeon via the zone catalog.
    /// Broadcasts <c>DungeonEntered</c> to the zone group (or back to the caller when not in a zone).
    /// </summary>
    /// <param name="dungeonSlug">The slug / zone ID of the dungeon (e.g. <c>"dungeon-grotto"</c>).</param>
    public async Task EnterDungeon(string dungeonSlug)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before EnterDungeon");
            return;
        }

        try
        {
            var result = await _mediator.Send(new EnterDungeonHubCommand(characterId, dungeonSlug));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to enter dungeon");
                return;
            }

            var payload = new
            {
                CharacterId = characterId,
                DungeonId   = result.DungeonId,
                DungeonSlug = dungeonSlug,
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("DungeonEntered", payload);
            else
                await Clients.Caller.SendAsync("DungeonEntered", payload);

            _logger.LogInformation(
                "Character {CharacterId} entered dungeon {DungeonId}",
                characterId, result.DungeonId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EnterDungeon for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to enter dungeon");
        }
    }

    /// <summary>Visits the merchant shop at the current zone.</summary>
    /// <param name="request">Request containing the zone ID to visit.</param>
    public async Task VisitShop(VisitShopHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before VisitShop");
            return;
        }

        try
        {
            var result = await _mediator.Send(new VisitShopHubCommand(characterId, request.ZoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to visit shop");
                return;
            }

            await Clients.Caller.SendAsync("ShopVisited", new
            {
                CharacterId = characterId,
                ZoneId      = result.ZoneId,
                ZoneName    = result.ZoneName,
            });

            _logger.LogInformation(
                "Character {CharacterId} visited shop at zone {ZoneId}",
                characterId, result.ZoneId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VisitShop for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to visit shop");
        }
    }

    /// <summary>
    /// Returns the DB-driven item catalog for the current zone's merchant shop.
    /// Sends <c>ShopCatalog</c> to the caller on success.
    /// </summary>
    public async Task GetShopCatalog()
    {
        if (!TryGetCharacterId(out _))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GetShopCatalog");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;

        try
        {
            var result = await _mediator.Send(new GetShopCatalogHubCommand(zoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to load shop catalog");
                return;
            }

            await Clients.Caller.SendAsync("ShopCatalog", new
            {
                ZoneId = zoneId,
                Items  = result.Items,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetShopCatalog for zone {ZoneId}", zoneId);
            await Clients.Caller.SendAsync("Error", "Failed to load shop catalog");
        }
    }

    /// <summary>
    /// Purchases one unit of an item from the current zone's merchant.
    /// Sends <c>ItemPurchased</c> to the caller on success.
    /// </summary>
    /// <param name="itemRef">The item slug to purchase.</param>
    public async Task BuyItem(string itemRef)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before BuyItem");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;

        try
        {
            var result = await _mediator.Send(new BuyItemHubCommand(characterId, zoneId, itemRef));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to buy item");
                return;
            }

            await Clients.Caller.SendAsync("ItemPurchased", new
            {
                CharacterId   = characterId,
                result.ItemRef,
                result.GoldSpent,
                result.RemainingGold,
            });

            _logger.LogInformation(
                "Character {CharacterId} bought '{ItemRef}' for {Gold} gold",
                characterId, itemRef, result.GoldSpent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BuyItem for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to buy item");
        }
    }

    /// <summary>
    /// Sells one unit of an item from the character's inventory to the current zone's merchant.
    /// Sends <c>ItemSold</c> to the caller on success.
    /// </summary>
    /// <param name="itemRef">The item slug to sell.</param>
    public async Task SellItem(string itemRef)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before SellItem");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;

        try
        {
            var result = await _mediator.Send(new SellItemHubCommand(characterId, zoneId, itemRef));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to sell item");
                return;
            }

            await Clients.Caller.SendAsync("ItemSold", new
            {
                CharacterId  = characterId,
                result.ItemRef,
                result.GoldReceived,
                result.NewGoldTotal,
            });

            _logger.LogInformation(
                "Character {CharacterId} sold '{ItemRef}' for {Gold} gold",
                characterId, itemRef, result.GoldReceived);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SellItem for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to sell item");
        }
    }

    /// <summary>
    /// Drops one unit of an item from the character's inventory (permanently removes it).
    /// Sends <c>ItemDropped</c> to the caller on success.
    /// </summary>
    /// <param name="itemRef">The item slug to drop.</param>
    public async Task DropItem(string itemRef)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before DropItem");
            return;
        }

        try
        {
            var result = await _mediator.Send(new DropItemHubCommand(characterId, itemRef));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to drop item");
                return;
            }

            await Clients.Caller.SendAsync("ItemDropped", new
            {
                CharacterId       = characterId,
                result.ItemRef,
                result.RemainingQuantity,
            });

            _logger.LogInformation(
                "Character {CharacterId} dropped '{ItemRef}'; {Remaining} remaining",
                characterId, itemRef, result.RemainingQuantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DropItem for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to drop item");
        }
    }

    /// <summary>
    /// Loads the quest log for the current character and sends <c>QuestLogReceived</c> to the caller.
    /// </summary>
    public async Task GetQuestLog()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GetQuestLog");
            return;
        }

        try
        {
            var result = await _mediator.Send(new GetQuestLogHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to load quest log");
                return;
            }

            await Clients.Caller.SendAsync("QuestLogReceived", result.Quests);

            _logger.LogInformation(
                "Quest log sent to character {CharacterId}: {Count} entries",
                characterId, result.Quests.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuestLog for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to load quest log");
        }
    }

    /// <summary>
    /// Moves the active character to a specific location within their current zone.
    /// Broadcasts <c>LocationEntered</c> to the zone group including available connections.
    /// Broadcasts <c>ZoneLocationUnlocked</c> to the caller for each passively discovered hidden location.
    /// </summary>
    /// <param name="request">Request containing the slug of the target zone location.</param>
    public async Task NavigateToLocation(NavigateToLocationHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before NavigateToLocation");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;
        var zoneGroupForNav = TryGetCurrentZoneGroup(out var zg) ? zg : string.Empty;

        try
        {
            var result = await _mediator.Send(new NavigateToLocationHubCommand(characterId, request.LocationSlug, zoneId, zoneGroupForNav));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to navigate to location");
                return;
            }

            var payload = new
            {
                CharacterId          = characterId,
                LocationSlug         = result.LocationSlug,
                LocationDisplayName  = result.LocationDisplayName,
                TypeKey              = result.TypeKey,
                SpawnedEnemies = result.SpawnedEnemies.Select(e => new
                {
                    e.Id, e.Name, e.Level, e.CurrentHealth, e.MaxHealth,
                }),
            };

            if (TryGetCurrentZoneGroup(out var zoneGroup))
                await Clients.Group(zoneGroup).SendAsync("LocationEntered", payload);
            else
                await Clients.Caller.SendAsync("LocationEntered", payload);

            // Notify the caller about each passively discovered location.
            foreach (var discovery in result.PassiveDiscoveries)
            {
                await Clients.Caller.SendAsync("ZoneLocationUnlocked", new
                {
                    CharacterId         = characterId,
                    LocationSlug        = discovery.Slug,
                    LocationDisplayName = discovery.DisplayName,
                    TypeKey             = discovery.TypeKey,
                    UnlockSource        = "skill_check_passive",
                });
            }

            _logger.LogInformation(
                "Character {CharacterId} navigated to {LocationSlug} in zone {ZoneId}",
                characterId, result.LocationSlug, zoneId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NavigateToLocation for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to navigate to location");
        }
    }

    /// <summary>
    /// Explicitly unlocks a hidden zone location for the active character.
    /// Broadcasts <c>ZoneLocationUnlocked</c> to the caller on success.
    /// </summary>
    /// <param name="request">Request containing the location slug and unlock source.</param>
    public async Task UnlockZoneLocation(UnlockZoneLocationHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before UnlockZoneLocation");
            return;
        }

        try
        {
            var result = await _mediator.Send(new UnlockZoneLocationHubCommand(characterId, request.LocationSlug, request.UnlockSource));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to unlock location");
                return;
            }

            if (!result.WasAlreadyUnlocked)
            {
                await Clients.Caller.SendAsync("ZoneLocationUnlocked", new
                {
                    CharacterId         = characterId,
                    LocationSlug        = result.LocationSlug,
                    LocationDisplayName = result.LocationDisplayName,
                    TypeKey             = result.TypeKey,
                    UnlockSource        = request.UnlockSource,
                });
            }

            _logger.LogInformation(
                "Character {CharacterId} unlocked location {LocationSlug} via {UnlockSource}",
                characterId, result.LocationSlug, request.UnlockSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UnlockZoneLocation for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to unlock location");
        }
    }

    /// <summary>
    /// Performs an active area search in the character's current zone, rolling for hidden locations
    /// with <c>UnlockType = "skill_check_active"</c>.
    /// Broadcasts <c>AreaSearched</c> to the caller with the roll result and any discoveries.
    /// Broadcasts <c>ZoneLocationUnlocked</c> to the caller for each newly found location.
    /// </summary>
    public async Task SearchArea()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before SearchArea");
            return;
        }

        var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;

        if (string.IsNullOrEmpty(zoneId))
        {
            await Clients.Caller.SendAsync("Error", "EnterZone must be called before SearchArea");
            return;
        }

        try
        {
            var result = await _mediator.Send(new SearchAreaHubCommand(characterId, zoneId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Search failed");
                return;
            }

            await Clients.Caller.SendAsync("AreaSearched", new
            {
                CharacterId = characterId,
                result.RollValue,
                result.AnyFound,
                Discovered  = result.Discovered.Select(d => new { d.Slug, d.DisplayName, d.TypeKey }),
            });

            foreach (var discovery in result.Discovered)
            {
                await Clients.Caller.SendAsync("ZoneLocationUnlocked", new
                {
                    CharacterId         = characterId,
                    LocationSlug        = discovery.Slug,
                    LocationDisplayName = discovery.DisplayName,
                    TypeKey             = discovery.TypeKey,
                    UnlockSource        = "skill_check_active",
                });
            }

            _logger.LogInformation(
                "Character {CharacterId} searched area in zone {ZoneId} — roll {Roll}, found {Count}",
                characterId, zoneId, result.RollValue, result.Discovered.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchArea for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Area search failed");
        }
    }

    /// <summary>Fetches the active character's inventory and sends it back to the caller as <c>InventoryLoaded</c>.</summary>
    public async Task GetInventory()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before GetInventory");
            return;
        }

        try
        {
            var result = await _mediator.Send(new GetInventoryHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to load inventory");
                return;
            }

            await Clients.Caller.SendAsync("InventoryLoaded", new
            {
                CharacterId = characterId,
                Items       = result.Items,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetInventory for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to load inventory");
        }
    }

    // Combat

    /// <summary>
    /// Initiates combat between the active character and a live enemy at the current location.
    /// Broadcasts <c>CombatStarted</c> to the caller and <c>EnemyEngaged</c> to the zone group.
    /// </summary>
    /// <param name="request">Request containing the location slug and enemy instance ID.</param>
    public async Task EngageEnemy(EngageEnemyHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before EngageEnemy");
            return;
        }

        var zoneGroup    = TryGetCurrentZoneGroup(out var zg) ? zg : string.Empty;
        var locationSlug = Context.Items.TryGetValue("CurrentLocationSlug", out var ls) && ls is string lss ? lss : request.LocationSlug;

        try
        {
            var result = await _mediator.Send(
                new EngageEnemyHubCommand(characterId, zoneGroup, locationSlug, request.EnemyId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Failed to engage enemy");
                return;
            }

            await Clients.Caller.SendAsync("CombatStarted", new
            {
                CharacterId  = characterId,
                result.EnemyId,
                result.EnemyName,
                result.EnemyLevel,
                result.EnemyCurrentHealth,
                result.EnemyMaxHealth,
                result.EnemyAbilityNames,
            });

            if (!string.IsNullOrEmpty(zoneGroup))
            {
                await Clients.OthersInGroup(zoneGroup).SendAsync("EnemyEngaged", new
                {
                    CharacterId = characterId,
                    result.EnemyId,
                    result.EnemyName,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EngageEnemy for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Failed to engage enemy");
        }
    }

    /// <summary>
    /// Performs a basic melee attack against the active character's engaged enemy.
    /// Broadcasts <c>CombatTurn</c> to the caller and, on enemy death, <c>EnemyDefeated</c> to the zone group.
    /// </summary>
    public async Task AttackEnemy()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before AttackEnemy");
            return;
        }

        try
        {
            var result = await _mediator.Send(new AttackEnemyHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Attack failed");
                return;
            }

            await Clients.Caller.SendAsync("CombatTurn", new
            {
                Action               = "attack",
                result.PlayerDamage,
                result.EnemyRemainingHealth,
                result.EnemyDefeated,
                result.EnemyDamage,
                result.EnemyAbilityUsed,
                result.PlayerRemainingHealth,
                result.PlayerDefeated,
                result.PlayerHardcoreDeath,
                result.XpEarned,
                result.GoldEarned,
            });

            if (result.EnemyDefeated && TryGetCurrentZoneGroup(out var zoneGroup))
            {
                await Clients.OthersInGroup(zoneGroup).SendAsync("EnemyDefeated", new
                {
                    CharacterId = characterId,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AttackEnemy for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Attack failed");
        }
    }

    /// <summary>
    /// Puts the active character in a defending stance for the current combat turn, reducing incoming damage.
    /// Broadcasts <c>CombatTurn</c> to the caller.
    /// </summary>
    public async Task DefendAction()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before DefendAction");
            return;
        }

        try
        {
            var result = await _mediator.Send(new DefendActionHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Defend failed");
                return;
            }

            await Clients.Caller.SendAsync("CombatTurn", new
            {
                Action               = "defend",
                result.EnemyDamage,
                result.EnemyAbilityUsed,
                result.PlayerRemainingHealth,
                result.PlayerDefeated,
                result.PlayerHardcoreDeath,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DefendAction for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Defend failed");
        }
    }

    /// <summary>
    /// Attempts to flee from active combat (50% success chance).
    /// On success, broadcasts <c>CombatEnded</c> to the caller.
    /// On failure, broadcasts <c>CombatTurn</c> with the enemy counter-attack result.
    /// </summary>
    public async Task FleeFromCombat()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before FleeFromCombat");
            return;
        }

        try
        {
            var result = await _mediator.Send(new FleeFromCombatHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Flee failed");
                return;
            }

            if (result.Fled)
            {
                await Clients.Caller.SendAsync("CombatEnded", new { CharacterId = characterId, Reason = "fled" });
            }
            else
            {
                await Clients.Caller.SendAsync("CombatTurn", new
                {
                    Action               = "flee_failed",
                    result.EnemyDamage,
                    result.EnemyAbilityUsed,
                    result.PlayerRemainingHealth,
                    result.PlayerDefeated,
                    result.PlayerHardcoreDeath,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FleeFromCombat for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Flee failed");
        }
    }

    /// <summary>
    /// Activates a named ability in combat, dealing damage or restoring health before the enemy counter-attacks.
    /// Broadcasts <c>CombatTurn</c> to the caller and <c>EnemyDefeated</c> to the zone group on kill.
    /// </summary>
    /// <param name="request">Request containing the ability ID to use.</param>
    public async Task UseAbilityInCombat(UseAbilityInCombatHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before UseAbilityInCombat");
            return;
        }

        try
        {
            var result = await _mediator.Send(new UseAbilityInCombatHubCommand(characterId, request.AbilityId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Ability use failed");
                return;
            }

            await Clients.Caller.SendAsync("CombatTurn", new
            {
                Action               = "ability",
                result.AbilityId,
                result.AbilityDamage,
                result.HealthRestored,
                result.ManaCost,
                result.PlayerRemainingMana,
                result.EnemyRemainingHealth,
                result.EnemyDefeated,
                result.EnemyDamage,
                result.EnemyAbilityUsed,
                result.PlayerRemainingHealth,
                result.PlayerDefeated,
                result.PlayerHardcoreDeath,
                result.XpEarned,
                result.GoldEarned,
            });

            if (result.EnemyDefeated && TryGetCurrentZoneGroup(out var zoneGroup))
            {
                await Clients.OthersInGroup(zoneGroup).SendAsync("EnemyDefeated", new
                {
                    CharacterId = characterId,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UseAbilityInCombat for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Ability use failed");
        }
    }

    /// <summary>
    /// Respawns the active character after death in normal mode.
    /// Restores a portion of HP and full mana; broadcasts <c>CharacterRespawned</c> to the caller.
    /// </summary>
    public async Task Respawn()
    {
        if (!TryGetCharacterId(out var characterId))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before Respawn");
            return;
        }

        try
        {
            var result = await _mediator.Send(new RespawnHubCommand(characterId));

            if (!result.Success)
            {
                await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Respawn failed");
                return;
            }

            await Clients.Caller.SendAsync("CharacterRespawned", new
            {
                CharacterId   = characterId,
                result.CurrentHealth,
                result.CurrentMana,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Respawn for character {CharacterId}", characterId);
            await Clients.Caller.SendAsync("Error", "Respawn failed");
        }
    }

    // Helpers
    private async Task LeaveCurrentZoneAsync(string connectionId, bool notifyPeers)
    {
        var session = await _playerSessionRepo.GetByConnectionIdAsync(connectionId);
        if (session is null) return;

        var zoneId        = session.ZoneId;
        var characterId   = session.CharacterId;
        var characterName = session.CharacterName;

        await _playerSessionRepo.RemoveAsync(session);

        // Player is on the region map — only leave the region group, no zone cleanup needed
        if (zoneId is null)
        {
            if (Context.Items.TryGetValue("CurrentRegionId", out var rid) && rid is string regionMapId)
                await Groups.RemoveFromGroupAsync(connectionId, RegionGroup(regionMapId));
            return;
        }

        // Untrack player; if zone is now empty of players, despawn all entities
        _entityTracker.UntrackPlayer(zoneId, characterId);
        if (_entityTracker.GetPlayerPositions(zoneId).Count == 0)
            _entityTracker.ClearZone(zoneId);

        // Use the stored group name so Wilderness players leave the correct difficulty-scoped group.
        var groupName = Context.Items.TryGetValue("CurrentZoneGroupName", out var gn) && gn is string gs
            ? gs
            : $"zone:{zoneId}";
        await Groups.RemoveFromGroupAsync(connectionId, groupName);

        if (notifyPeers)
        {
            await Clients.Group(groupName).SendAsync("PlayerLeft", new
            {
                CharacterId   = characterId,
                CharacterName = characterName,
                ZoneId        = zoneId,
            });
        }

        _logger.LogInformation("Character {Name} left zone {ZoneId}", characterName, zoneId);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the server's current UTC timestamp in Unix milliseconds.
    /// Used by the client to measure round-trip latency (ping).
    /// </summary>
    public Task<long> Ping() =>
        Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // ── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts a chat message to all players currently in the caller's zone.
    /// </summary>
    /// <param name="request">The message payload.</param>
    public async Task SendZoneMessage(SendZoneChatMessageHubRequest request)
    {
        if (!TryGetCharacterId(out var characterId) || !TryGetCharacterName(out var characterName))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before SendZoneMessage");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
            return;

        var dto = new ChatMessageHubDto(
            Channel: "Zone",
            Sender: characterName,
            Message: request.Message.Trim(),
            Timestamp: DateTimeOffset.UtcNow);

        if (TryGetCurrentZoneGroup(out var zoneGroup))
            await Clients.Group(zoneGroup).SendAsync("ReceiveChatMessage", dto);
        else
            await Clients.Caller.SendAsync("ReceiveChatMessage", dto);
    }

    /// <summary>
    /// Broadcasts a chat message to all connected players across all zones.
    /// </summary>
    /// <param name="request">The message payload.</param>
    public async Task SendGlobalMessage(SendGlobalChatMessageHubRequest request)
    {
        if (!TryGetCharacterId(out _) || !TryGetCharacterName(out var characterName))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before SendGlobalMessage");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
            return;

        var dto = new ChatMessageHubDto(
            Channel: "Global",
            Sender: characterName,
            Message: request.Message.Trim(),
            Timestamp: DateTimeOffset.UtcNow);

        await Clients.All.SendAsync("ReceiveChatMessage", dto);
    }

    /// <summary>
    /// Sends a private whisper message from the caller to a specific online character.
    /// Delivers the message to the target and echoes it back to the sender.
    /// </summary>
    /// <param name="request">Target character name and message text.</param>
    public async Task SendWhisper(SendWhisperHubRequest request)
    {
        if (!TryGetCharacterId(out _) || !TryGetCharacterName(out var senderName))
        {
            await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before SendWhisper");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.TargetCharacterName))
            return;

        var targetSession = await _playerSessionRepo.GetByCharacterNameAsync(request.TargetCharacterName);
        if (targetSession is null)
        {
            await Clients.Caller.SendAsync("Error", $"Player '{request.TargetCharacterName}' is not online.");
            return;
        }

        var dto = new ChatMessageHubDto(
            Channel: "Whisper",
            Sender: senderName,
            Message: request.Message.Trim(),
            Timestamp: DateTimeOffset.UtcNow);

        await Clients.Client(targetSession.ConnectionId).SendAsync("ReceiveChatMessage", dto);

        // Echo to sender so they see their own whisper in the chat log
        var echoDto = dto with { Sender = $"To {request.TargetCharacterName}" };
        await Clients.Caller.SendAsync("ReceiveChatMessage", echoDto);
    }

    /// <summary>
    /// Spawns a small set of enemies into the zone when the first player enters.
    /// Picks up to 3 archetypes from the enemy repository and places them at random
    /// non-blocked, non-spawn-point tiles.
    /// </summary>
    private async Task<IReadOnlyList<ZoneEntitySnapshot>> SpawnEnemiesForZoneAsync(string zoneId, TiledMap map)
    {
        try
        {
            var archetypes = (await _enemyRepo.GetAllAsync()).Take(3).ToList();
            if (archetypes.Count == 0) return [];

            var spawnPointSet = map.GetSpawnPoints()
                .Select(p => (p.TileX, p.TileY))
                .ToHashSet();

            var rng = new Random(zoneId.GetHashCode());
            var snapshots = new List<ZoneEntitySnapshot>(archetypes.Count);

            foreach (var archetype in archetypes)
            {
                // Pick a random non-blocked tile that isn't a player spawn point
                var (ex, ey) = FindSpawnTile(map, spawnPointSet, rng);
                var snapshot = new ZoneEntitySnapshot(
                    EntityId:      Guid.NewGuid(),
                    EntityType:    "enemy",
                    ArchetypeSlug: archetype.Slug,
                    SpriteKey:     archetype.Slug,
                    TileX:         ex,
                    TileY:         ey,
                    Direction:     "S",
                    MaxHealth:     archetype.MaxHealth,
                    CurrentHealth: archetype.Health);
                snapshots.Add(snapshot);
            }

            _entityTracker.SetEntities(zoneId, snapshots);
            return snapshots;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn enemies for zone {ZoneId}", zoneId);
            return [];
        }
    }

    private static (int x, int y) FindSpawnTile(TiledMap map, HashSet<(int, int)> exclusions, Random rng)
    {
        // Make up to 50 attempts to find a valid non-blocked, non-spawn-point tile
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var x = rng.Next(1, map.Width - 1);
            var y = rng.Next(1, map.Height - 1);
            if (!map.IsBlocked(x, y) && !exclusions.Contains((x, y)))
                return (x, y);
        }
        // Fallback: top-left-ish corner
        return (2, 2);
    }

    private static string ComputeZoneGroup(ZoneType zoneType, string zoneId, string difficultyMode) =>
        zoneType == ZoneType.Wilderness ? $"zone:{zoneId}_{difficultyMode}" : $"zone:{zoneId}";

    private bool TryGetCurrentZoneGroup([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? groupName)
    {
        if (Context.Items.TryGetValue("CurrentZoneGroupName", out var val) && val is string s)
        {
            groupName = s;
            return true;
        }
        groupName = null;
        return false;
    }

    private static string AccountGroup(Guid accountId) => $"account:{accountId}";

    private static string RegionGroup(string regionId) => $"region:{regionId}";

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

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.RestAtLocation"/>.</summary>
/// <param name="LocationId">ID of the inn or rest-point location.</param>
/// <param name="CostInGold">Gold deducted for the rest (default: 10).</param>
public record RestAtLocationHubRequest(string LocationId, int CostInGold = 10);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.AwardSkillXp"/>.</summary>
/// <param name="SkillId">Skill identifier (e.g. <c>"swordsmanship"</c>, <c>"herbalism"</c>).</param>
/// <param name="Amount">XP amount to award. Must be positive.</param>
public record AwardSkillXpHubRequest(string SkillId, int Amount);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.EquipItem"/>.</summary>
/// <param name="Slot">Slot name (e.g. <c>"MainHand"</c>, <c>"Head"</c>). Must be a known slot.</param>
/// <param name="ItemRef">Item-reference slug to equip, or <see langword="null"/> to clear the slot.</param>
public record EquipItemHubRequest(string Slot, string? ItemRef);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.GainExperience"/>.</summary>
/// <param name="Amount">Positive number of experience points to award.</param>
/// <param name="Source">Optional label for the XP source (e.g. <c>"Combat"</c>, <c>"Quest"</c>).</param>
public record GainExperienceHubRequest(int Amount, string? Source = null);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.AddGold"/>.</summary>
/// <param name="Amount">Gold to add (positive) or spend (negative). Cannot be zero.</param>
/// <param name="Source">Optional label for the gold source or sink (e.g. <c>"Loot"</c>, <c>"Quest"</c>).</param>
public record AddGoldHubRequest(int Amount, string? Source = null);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.TakeDamage"/>.</summary>
/// <param name="DamageAmount">Positive number of hit points to remove.</param>
/// <param name="Source">Optional label for the damage source (e.g. <c>"Enemy"</c>, <c>"Trap"</c>).</param>
public record TakeDamageHubRequest(int DamageAmount, string? Source = null);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.VisitShop"/>.</summary>
/// <param name="ZoneId">ID of the zone whose merchant to visit.</param>
public record VisitShopHubRequest(string ZoneId);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.NavigateToLocation"/>.</summary>
/// <param name="LocationSlug">Slug of the target zone location (e.g. <c>"fenwick-market"</c>).</param>
public record NavigateToLocationHubRequest(string LocationSlug);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.UnlockZoneLocation"/>.</summary>
/// <param name="LocationSlug">Slug of the hidden location to unlock.</param>
/// <param name="UnlockSource">How the unlock was triggered (e.g. <c>"quest"</c>, <c>"item"</c>, <c>"manual"</c>).</param>
public record UnlockZoneLocationHubRequest(string LocationSlug, string UnlockSource);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.SendZoneMessage"/>.</summary>
/// <param name="Message">The chat message text. Must not be empty or whitespace.</param>
public record SendZoneChatMessageHubRequest(string Message);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.SendGlobalMessage"/>.</summary>
/// <param name="Message">The chat message text. Must not be empty or whitespace.</param>
public record SendGlobalChatMessageHubRequest(string Message);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.SendWhisper"/>.</summary>
/// <param name="TargetCharacterName">The display name of the target character.</param>
/// <param name="Message">The whisper message text. Must not be empty or whitespace.</param>
public record SendWhisperHubRequest(string TargetCharacterName, string Message);

/// <summary>Server-to-client chat message payload for the <c>ReceiveChatMessage</c> event.</summary>
/// <param name="Channel">Chat channel: <c>Zone</c>, <c>Global</c>, <c>Whisper</c>, or <c>System</c>.</param>
/// <param name="Sender">Display name of the character who sent the message.</param>
/// <param name="Message">The message text.</param>
/// <param name="Timestamp">UTC time the message was sent.</param>
public record ChatMessageHubDto(string Channel, string Sender, string Message, DateTimeOffset Timestamp);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.EngageEnemy"/>.</summary>
/// <param name="LocationSlug">Slug of the current zone location (used as fallback if not stored in context).</param>
/// <param name="EnemyId">Unique instance ID of the enemy to engage.</param>
public record EngageEnemyHubRequest(string LocationSlug, Guid EnemyId);

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.UseAbilityInCombat"/>.</summary>
/// <param name="AbilityId">Identifier of the ability to activate.</param>
public record UseAbilityInCombatHubRequest(string AbilityId);

