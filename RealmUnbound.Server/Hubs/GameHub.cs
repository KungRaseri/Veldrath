using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Hubs;

/// <summary>
/// Real-time game hub. Clients must connect with a valid JWT (passed as query-string
/// <c>access_token</c> parameter, which the JWT middleware picks up from the request).
/// </summary>
[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly ICharacterRepository _characterRepo;

    public GameHub(ILogger<GameHub> logger, ICharacterRepository characterRepo)
    {
        _logger = logger;
        _characterRepo = characterRepo;
    }

    public override async Task OnConnectedAsync()
    {
        var username = Context.User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? "unknown";
        _logger.LogInformation("Client connected: {ConnectionId} (user: {Username})", Context.ConnectionId, username);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Attach a character to this connection. Must be called after connecting before
    /// entering the game world. Returns success/failure via the <c>CharacterSelected</c>
    /// or <c>Error</c> client events.
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

        _logger.LogInformation(
            "Account {AccountId} selected character {CharacterName} ({CharacterId}) on connection {ConnectionId}",
            accountId, character.Name, character.Id, Context.ConnectionId);

        await Clients.Caller.SendAsync("CharacterSelected", new
        {
            character.Id,
            character.Name,
            character.ClassName,
            character.Level,
            SelectedAt = DateTimeOffset.UtcNow,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid GetAccountId()
    {
        var value = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from JWT.");
        return Guid.Parse(value);
    }
}

