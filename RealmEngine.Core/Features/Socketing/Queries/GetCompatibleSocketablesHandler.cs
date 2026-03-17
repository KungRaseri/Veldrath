using MediatR;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Handler for getting compatible socketable items for a socket type.
/// </summary>
public class GetCompatibleSocketablesHandler : IRequestHandler<GetCompatibleSocketablesQuery, CompatibleSocketablesResult>
{
    private readonly ILogger<GetCompatibleSocketablesHandler> _logger;

    public GetCompatibleSocketablesHandler(ILogger<GetCompatibleSocketablesHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the compatible socketables query.
    /// </summary>
    /// <param name="request">The compatibility query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of compatible socketable items.</returns>
    public Task<CompatibleSocketablesResult> Handle(GetCompatibleSocketablesQuery request, CancellationToken cancellationToken)
    {
        var result = new CompatibleSocketablesResult
        {
            Success = true,
            SocketType = request.SocketType
        };

        try
        {
            // Note: In production, this would query the player's inventory or available socketables
            // For now, we return an empty list with proper structure
            
            _logger.LogInformation("Querying compatible socketables for socket type {SocketType}", request.SocketType);

            // Example filtering logic (would query actual inventory in production):
            // 1. Filter by socket type compatibility
            // 2. Filter by category if specified
            // 3. Filter by minimum rarity if specified
            // 4. Sort by rarity/level/value

            var items = new List<SocketableItemDto>();

            // IMPLEMENTATION NOTE: This handler currently returns an empty list for demonstration.
            // Production implementation would integrate with IInventoryService:
            //
            // var allSocketables = await _inventoryService.GetSocketableItemsAsync(request.CharacterName);
            // items = allSocketables
            //     .Where(s => s.SocketType == request.SocketType || request.SocketType == SocketType.Prismatic)
            //     .Where(s => request.Category == null || s.Category == request.Category)
            //     .Where(s => request.MinimumRarity == null || s.Rarity >= request.MinimumRarity)
            //     .Select(s => MapToDto(s))
            //     .OrderByDescending(s => s.Rarity)
            //     .ThenByDescending(s => s.Level)
            //     .ToList();

            result.Items = items;
            result.TotalCount = items.Count;

            // AI-suggested items based on player stats/level (future enhancement)
            result.SuggestedItems = new List<SocketableItemDto>();

            _logger.LogDebug("Found {Count} compatible socketables for {SocketType}",
                result.TotalCount, request.SocketType);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying compatible socketables");
            result.Success = false;
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Maps a socketable item to its DTO representation.
    /// </summary>
    /// <param name="socketable">The socketable item.</param>
    /// <returns>The DTO representation.</returns>
    private static SocketableItemDto MapToDto(ISocketable socketable)
    {
        var dto = new SocketableItemDto
        {
            Name = socketable.Name,
            SocketType = socketable.SocketType,
            Description = socketable.Description ?? string.Empty,
            Category = GetSocketableCategory(socketable)
        };

        // Convert traits to stat bonuses
        foreach (var trait in socketable.Traits)
        {
            var value = trait.Value.Type == TraitType.Number 
                ? trait.Value.AsDouble() 
                : 0.0;
            var isPercentage = value < 1.0 && value > 0;
            
            dto.Bonuses.Add(new StatBonusDto
            {
                StatName = trait.Key,
                Value = value,
                IsPercentage = isPercentage,
                DisplayText = isPercentage 
                    ? $"+{value * 100:F1}% {trait.Key}" 
                    : $"+{value} {trait.Key}"
            });
        }

        return dto;
    }

    /// <summary>
    /// Determines the category of a socketable item.
    /// </summary>
    /// <param name="socketable">The socketable item.</param>
    /// <returns>The category string.</returns>
    private static string GetSocketableCategory(ISocketable socketable)
    {
        return socketable switch
        {
            Gem => "Gem",
            Rune => "Rune",
            Crystal => "Crystal",
            Orb => "Orb",
            _ => "Unknown"
        };
    }
}
