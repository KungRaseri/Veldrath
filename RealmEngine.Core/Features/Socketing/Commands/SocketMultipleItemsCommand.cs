using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Command to socket multiple items in one operation (for UI batch processing).
/// </summary>
public record SocketMultipleItemsCommand(
    string EquipmentItemId,
    List<SocketOperation> Operations) : IRequest<SocketMultipleItemsResult>;

/// <summary>
/// Single socket operation within a batch.
/// </summary>
public record SocketOperation(
    int SocketIndex,
    ISocketable SocketableItem);

/// <summary>
/// Result of batch socketing operation.
/// </summary>
public class SocketMultipleItemsResult
{
    /// <summary>Gets or sets a value indicating whether all operations succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the overall result message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets results for each individual operation.</summary>
    public List<SingleSocketResult> Results { get; set; } = new();
    
    /// <summary>Gets or sets the number of successful operations.</summary>
    public int SuccessCount { get; set; }
    
    /// <summary>Gets or sets the number of failed operations.</summary>
    public int FailureCount { get; set; }
    
    /// <summary>Gets or sets the total traits applied from all socketings.</summary>
    public Dictionary<string, TraitValue> TotalAppliedTraits { get; set; } = new();
}

/// <summary>
/// Result of a single socket operation within a batch.
/// </summary>
public class SingleSocketResult
{
    /// <summary>Gets or sets the socket index.</summary>
    public int SocketIndex { get; set; }
    
    /// <summary>Gets or sets a value indicating whether this operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the socketable item name.</summary>
    public string? SocketableItemName { get; set; }
}
