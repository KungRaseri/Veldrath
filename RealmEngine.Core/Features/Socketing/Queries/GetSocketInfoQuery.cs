using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Query to get socket information for an equipment item.
/// </summary>
public record GetSocketInfoQuery(string EquipmentItemId) : IRequest<SocketInfoResult>;

/// <summary>
/// Result containing socket information for an equipment item.
/// </summary>
public class SocketInfoResult
{
    /// <summary>Gets or sets a value indicating whether the query succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the list of sockets organized by type.</summary>
    public Dictionary<SocketType, List<SocketDetailInfo>> SocketsByType { get; set; } = new();
    
    /// <summary>Gets or sets the total number of sockets.</summary>
    public int TotalSockets { get; set; }
    
    /// <summary>Gets or sets the number of filled sockets.</summary>
    public int FilledSockets { get; set; }
    
    /// <summary>Gets or sets the number of empty sockets.</summary>
    public int EmptySockets { get; set; }
    
    /// <summary>Gets or sets information about linked socket groups.</summary>
    public List<LinkedSocketGroup> LinkedGroups { get; set; } = new();
}

/// <summary>
/// Detailed information about a single socket.
/// </summary>
public class SocketDetailInfo
{
    /// <summary>Gets or sets the socket index in the item.</summary>
    public int Index { get; set; }
    
    /// <summary>Gets or sets the socket type.</summary>
    public SocketType Type { get; set; }
    
    /// <summary>Gets or sets a value indicating whether the socket is empty.</summary>
    public bool IsEmpty { get; set; }
    
    /// <summary>Gets or sets a value indicating whether the socket is locked.</summary>
    public bool IsLocked { get; set; }
    
    /// <summary>Gets or sets the link group (-1 if unlinked).</summary>
    public int LinkGroup { get; set; }
    
    /// <summary>Gets or sets the socketable item in this socket.</summary>
    public ISocketable? Content { get; set; }
}

/// <summary>
/// Information about a group of linked sockets.
/// </summary>
public class LinkedSocketGroup
{
    /// <summary>Gets or sets the link group identifier.</summary>
    public int LinkGroupId { get; set; }
    
    /// <summary>Gets or sets the socket indices in this linked group.</summary>
    public List<int> SocketIndices { get; set; } = new();
    
    /// <summary>Gets or sets the number of sockets in this link.</summary>
    public int LinkSize { get; set; }
    
    /// <summary>Gets or sets the bonus multiplier for this link (e.g., 1.1 for 10% bonus).</summary>
    public double BonusMultiplier { get; set; }
}
