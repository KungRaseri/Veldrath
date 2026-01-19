using MediatR;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Query to get the gold cost for socketing or removing a socketed item.
/// </summary>
public record GetSocketCostQuery(
    string EquipmentItemId,
    SocketCostType CostType,
    int SocketIndex = 0) : IRequest<SocketCostResult>;

/// <summary>
/// Type of socket operation for cost calculation.
/// </summary>
public enum SocketCostType
{
    /// <summary>Cost to socket an item.</summary>
    Socket,
    
    /// <summary>Cost to remove a socketed item.</summary>
    Remove,
    
    /// <summary>Cost to unlock a locked socket.</summary>
    Unlock
}

/// <summary>
/// Result containing socket operation cost information.
/// </summary>
public class SocketCostResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the gold cost for the operation.</summary>
    public int GoldCost { get; set; }
    
    /// <summary>Gets or sets the base cost before modifiers.</summary>
    public int BaseCost { get; set; }
    
    /// <summary>Gets or sets the cost multipliers applied.</summary>
    public List<CostModifier> Modifiers { get; set; } = new();
    
    /// <summary>Gets or sets a description of the cost calculation.</summary>
    public string CostDescription { get; set; } = string.Empty;
    
    /// <summary>Gets or sets a value indicating whether the player can afford this.</summary>
    public bool CanAfford { get; set; }
    
    /// <summary>Gets or sets the player's current gold (if checked).</summary>
    public int? PlayerGold { get; set; }
}

/// <summary>
/// Cost modifier information for transparency.
/// </summary>
public class CostModifier
{
    /// <summary>Gets or sets the modifier name (e.g., "Item Rarity", "Socket Count", "Reputation Discount").</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the multiplier value.</summary>
    public double Multiplier { get; set; }
    
    /// <summary>Gets or sets a description of this modifier.</summary>
    public string Description { get; set; } = string.Empty;
}
