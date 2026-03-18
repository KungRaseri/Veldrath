using RealmEngine.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Command to use a consumable item.
/// </summary>
public record UseItemCommand : IRequest<UseItemResult>
{
    /// <summary>
    /// Gets the player character using the item.
    /// </summary>
    public required Character Player { get; init; }
    
    /// <summary>
    /// Gets the item to use.
    /// </summary>
    public required Item Item { get; init; }
}

/// <summary>
/// Result of using an item.
/// </summary>
public record UseItemResult
{
    /// <summary>
    /// Gets a value indicating whether the use was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Gets a message describing the use result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// Gets the amount of health restored.
    /// </summary>
    public int HealthRestored { get; init; }
    
    /// <summary>
    /// Gets the amount of mana restored.
    /// </summary>
    public int ManaRestored { get; init; }
}

/// <summary>
/// Handler for UseItemCommand. Applies consumable item effects to the player character.
/// </summary>
public class UseItemHandler : IRequestHandler<UseItemCommand, UseItemResult>
{
    private readonly ILogger<UseItemHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseItemHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public UseItemHandler(ILogger<UseItemHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the use item command.
    /// </summary>
    /// <param name="request">The use item command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of using the item.</returns>
    public Task<UseItemResult> Handle(UseItemCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var item = request.Item;

        if (item.Type != ItemType.Consumable)
        {
            return Task.FromResult(new UseItemResult
            {
                Success = false,
                Message = $"{item.Name} is not a consumable item."
            });
        }

        if (!player.Inventory.Contains(item))
        {
            return Task.FromResult(new UseItemResult
            {
                Success = false,
                Message = $"{item.Name} is not in your inventory."
            });
        }

        int healthRestored = 0;
        int manaRestored = 0;
        string effectMsg;

        var effect = item.Effect?.ToLowerInvariant() ?? string.Empty;

        if (effect.StartsWith("heal") || effect == "restore_health")
        {
            var before = player.Health;
            player.Health = Math.Min(player.MaxHealth, player.Health + item.Power);
            healthRestored = player.Health - before;
            effectMsg = $"Restored {healthRestored} health.";
        }
        else if (effect.StartsWith("mana") || effect == "restore_mana" || effect == "restore")
        {
            var before = player.Mana;
            player.Mana = Math.Min(player.MaxMana, player.Mana + item.Power);
            manaRestored = player.Mana - before;
            effectMsg = $"Restored {manaRestored} mana.";
        }
        else if (effect == "full_restore")
        {
            healthRestored = player.MaxHealth - player.Health;
            manaRestored = player.MaxMana - player.Mana;
            player.Health = player.MaxHealth;
            player.Mana = player.MaxMana;
            effectMsg = "Fully restored health and mana.";
        }
        else
        {
            // Unknown effect — still consume but with a generic message
            effectMsg = $"Used {item.Name}.";
        }

        player.Inventory.Remove(item);
        _logger.LogInformation("Player {Name} used {Item}: {Effect}", player.Name, item.Name, effectMsg);

        return Task.FromResult(new UseItemResult
        {
            Success = true,
            Message = $"Used {item.Name}. {effectMsg}",
            HealthRestored = healthRestored,
            ManaRestored = manaRestored
        });
    }
}