using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Enchanting.Queries;

/// <summary>
/// Query to get cost and feasibility information for an enchanting or slot-unlock operation.
/// </summary>
public record GetEnchantmentCostQuery(
    Item Item,
    EnchantmentOperationType OperationType,
    int EnchantingSkillRank = 0) : IRequest<GetEnchantmentCostResult>;

/// <summary>
/// Type of enchanting operation to price.
/// </summary>
public enum EnchantmentOperationType
{
    /// <summary>Apply an enchantment scroll to fill the next available slot.</summary>
    ApplyEnchantment,

    /// <summary>Remove an existing player-applied enchantment using a removal scroll.</summary>
    RemoveEnchantment,

    /// <summary>Unlock a new slot using a socket crystal.</summary>
    UnlockSlot
}

/// <summary>
/// Result of an enchantment cost query.
/// </summary>
public class GetEnchantmentCostResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation type queried.</summary>
    public EnchantmentOperationType OperationType { get; set; }

    /// <summary>Gets or sets a value indicating whether the operation is currently possible.</summary>
    public bool IsPossible { get; set; }

    /// <summary>Gets or sets the reason the operation is blocked (empty if possible).</summary>
    public string BlockedReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the success rate for the operation (0–100; 100 for guaranteed operations).</summary>
    public double SuccessRate { get; set; }

    /// <summary>Gets or sets the Enchanting skill requirement for the operation (0 if none).</summary>
    public int RequiredSkill { get; set; }

    /// <summary>Gets or sets the consumable required (scroll name or crystal name).</summary>
    public string RequiredConsumable { get; set; } = string.Empty;
}
