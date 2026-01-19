using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Command to remove a socketed item from an equipment socket.
/// </summary>
public record RemoveSocketedItemCommand(
    string EquipmentItemId, 
    int SocketIndex,
    int GoldCost = 0) : IRequest<RemoveSocketedItemResult>;

/// <summary>
/// Result of removing a socketed item.
/// </summary>
public class RemoveSocketedItemResult
{
    /// <summary>Gets or sets a value indicating whether the removal operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the socketable item that was removed.</summary>
    public ISocketable? RemovedItem { get; set; }
    
    /// <summary>Gets or sets the traits that were removed from the equipment.</summary>
    public Dictionary<string, TraitValue> RemovedTraits { get; set; } = new();
    
    /// <summary>Gets or sets the gold cost paid for removal.</summary>
    public int GoldPaid { get; set; }
}
