using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Handler for calculating socket operation costs.
/// </summary>
public class GetSocketCostHandler : IRequestHandler<GetSocketCostQuery, SocketCostResult>
{
    // Base costs for different operations
    private const int BaseSocketCost = 10;
    private const int BaseRemoveCost = 20;
    private const int BaseUnlockCost = 100;

    private readonly ILogger<GetSocketCostHandler> _logger;

    public GetSocketCostHandler(ILogger<GetSocketCostHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the socket cost query.
    /// </summary>
    /// <param name="request">The cost query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cost calculation result.</returns>
    public Task<SocketCostResult> Handle(GetSocketCostQuery request, CancellationToken cancellationToken)
    {
        var result = new SocketCostResult
        {
            Success = true
        };

        try
        {
            // Calculate base cost based on operation type
            result.BaseCost = request.CostType switch
            {
                SocketCostType.Socket => BaseSocketCost,
                SocketCostType.Remove => BaseRemoveCost,
                SocketCostType.Unlock => BaseUnlockCost,
                _ => 0
            };

            // Apply modifiers (in production, these would come from jeweler level, player perks, etc.)
            var modifiers = new List<CostModifier>();

            // Socket position multiplier (later sockets cost more)
            if (request.SocketIndex > 0)
            {
                var positionMultiplier = 1.0 + (request.SocketIndex * 0.5);
                modifiers.Add(new CostModifier
                {
                    Name = "Socket Position",
                    Multiplier = positionMultiplier,
                    Description = $"Socket {request.SocketIndex + 1} costs more"
                });
            }

            // Master jeweler discount (example - would come from game state)
            // modifiers.Add(new CostModifier
            // {
            //     Name = "Master Jeweler",
            //     Multiplier = 0.8,
            //     Description = "10% discount from jeweler reputation"
            // });

            result.Modifiers = modifiers;

            // Calculate final cost
            double finalCost = result.BaseCost;
            foreach (var modifier in modifiers)
            {
                finalCost *= modifier.Multiplier;
            }

            result.GoldCost = (int)Math.Round(finalCost);

            // Build cost description
            result.CostDescription = result.Modifiers.Count > 0
                ? $"Base: {result.BaseCost}g, Final: {result.GoldCost}g (with modifiers)"
                : $"{result.GoldCost}g";

            // Check affordability (would check actual player gold in production)
            result.CanAfford = true; // Default - UI would override with actual player gold check
            result.PlayerGold = 0; // Would be populated from game state

            _logger.LogDebug("Socket cost calculated: {CostType} at index {Index} = {Cost}g",
                request.CostType, request.SocketIndex, result.GoldCost);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating socket cost");
            result.Success = false;
            result.CostDescription = $"Cost calculation failed: {ex.Message}";
            return Task.FromResult(result);
        }
    }
}
