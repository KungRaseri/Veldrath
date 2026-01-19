using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Query to get available socketable items that can fit into a specific socket type.
/// </summary>
public record GetCompatibleSocketablesQuery(
    SocketType SocketType,
    string? Category = null,
    ItemRarity? MinimumRarity = null) : IRequest<CompatibleSocketablesResult>;

/// <summary>
/// Result containing compatible socketable items.
/// </summary>
public class CompatibleSocketablesResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the socket type queried.</summary>
    public SocketType SocketType { get; set; }
    
    /// <summary>Gets or sets the list of compatible socketable items.</summary>
    public List<SocketableItemDto> Items { get; set; } = new();
    
    /// <summary>Gets or sets the total count of compatible items.</summary>
    public int TotalCount { get; set; }
    
    /// <summary>Gets or sets suggested items based on player level/stats (AI hints).</summary>
    public List<SocketableItemDto> SuggestedItems { get; set; } = new();
}
