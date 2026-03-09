using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace RealmUnbound.Server.Hubs;

public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    // private readonly IMediator _mediator; // TODO: inject when RealmEngine is wired

    public GameHub(ILogger<GameHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client calls this to identify a player session.
    /// Returns a session token on success.
    /// </summary>
    public async Task JoinGame(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("Error", "Player name is required.");
            return;
        }

        _logger.LogInformation("Player '{PlayerName}' joining game (ConnectionId: {ConnectionId})",
            playerName, Context.ConnectionId);

        // TODO: var sessionId = await _mediator.Send(new JoinGameCommand { PlayerName = playerName });

        await Clients.Caller.SendAsync("GameJoined", new
        {
            PlayerName = playerName,
            ConnectionId = Context.ConnectionId,
            JoinedAt = DateTimeOffset.UtcNow
        });
    }
}
