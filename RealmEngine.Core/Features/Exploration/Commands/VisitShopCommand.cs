using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Command to visit a shop in the current location.
/// </summary>
public record VisitShopCommand(string LocationId, string CharacterName) : IRequest<VisitShopResult>;

/// <summary>
/// Result of visiting a shop.
/// </summary>
public record VisitShopResult
{
    /// <summary>Gets whether the visit was successful.</summary>
    public bool Success { get; init; }
    
    /// <summary>Gets the merchant NPC at the shop.</summary>
    public NPC? Merchant { get; init; }
    
    /// <summary>Gets the shop inventory items.</summary>
    public List<Item> Inventory { get; init; } = new();
    
    /// <summary>Gets an error message if the visit failed.</summary>
    public string? ErrorMessage { get; init; }
}
