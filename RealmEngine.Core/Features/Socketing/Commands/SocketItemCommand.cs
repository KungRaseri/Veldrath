using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Command to socket an item (Gem, Rune, Crystal, or Orb) into an equipment socket.
/// </summary>
public record SocketItemCommand(
    string EquipmentItemId, 
    int SocketIndex, 
    ISocketable SocketableItem) : IRequest<SocketItemResult>;

/// <summary>
/// Result of socketing an item.
/// </summary>
public class SocketItemResult
{
    /// <summary>Gets or sets a value indicating whether the socket operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the socketed item that was added.</summary>
    public ISocketable? SocketedItem { get; set; }
    
    /// <summary>Gets or sets the traits that were applied from the socketed item.</summary>
    public Dictionary<string, TraitValue> AppliedTraits { get; set; } = new();
    
    /// <summary>Gets or sets a value indicating whether the socket is part of a linked group.</summary>
    public bool IsLinked { get; set; }
    
    /// <summary>Gets or sets the link bonus multiplier if applicable (e.g., 1.2 for 20% bonus).</summary>
    public double LinkBonusMultiplier { get; set; } = 1.0;
}
