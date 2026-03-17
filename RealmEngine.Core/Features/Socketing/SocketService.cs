using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing;

/// <summary>
/// Service for managing socket operations on items.
/// </summary>
public class SocketService
{
    private readonly ILogger<SocketService> _logger;

    public SocketService(ILogger<SocketService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates whether a socketable item can be placed in a specific socket.
    /// </summary>
    /// <param name="socket">The socket to validate.</param>
    /// <param name="socketableItem">The item to socket.</param>
    /// <returns>Validation result with success status and error message if applicable.</returns>
    public SocketValidationResult ValidateSocketing(Socket socket, ISocketable socketableItem)
    {
        // Check if socket is locked
        if (socket.IsLocked)
        {
            return new SocketValidationResult
            {
                IsValid = false,
                ErrorMessage = "Socket is locked and cannot be modified"
            };
        }
        
        // Check if socket is already filled
        if (socket.Content != null)
        {
            return new SocketValidationResult
            {
                IsValid = false,
                ErrorMessage = "Socket is already filled. Remove existing item first."
            };
        }
        
        // Check socket type compatibility
        if (!socket.CanAccept(socketableItem))
        {
            return new SocketValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Socket type mismatch: {socket.Type} socket cannot accept {socketableItem.SocketType} items"
            };
        }
        
        return new SocketValidationResult
        {
            IsValid = true,
            ErrorMessage = string.Empty
        };
    }
    
    /// <summary>
    /// Sockets an item into a socket slot.
    /// </summary>
    /// <param name="socket">The socket to fill.</param>
    /// <param name="socketableItem">The item to socket.</param>
    /// <returns>Result indicating success or failure.</returns>
    public SocketOperationResult SocketItem(Socket socket, ISocketable socketableItem)
    {
        var validation = ValidateSocketing(socket, socketableItem);
        if (!validation.IsValid)
        {
            return new SocketOperationResult
            {
                Success = false,
                Message = validation.ErrorMessage
            };
        }
        
        socket.Content = socketableItem;
        _logger.LogInformation("Socketed {ItemName} into {SocketType} socket", socketableItem.Name, socket.Type);
        
        return new SocketOperationResult
        {
            Success = true,
            Message = $"Successfully socketed {socketableItem.Name}"
        };
    }
    
    /// <summary>
    /// Removes a socketed item from a socket slot.
    /// </summary>
    /// <param name="socket">The socket to clear.</param>
    /// <returns>Result indicating success or failure with the removed item.</returns>
    public SocketRemovalResult RemoveSocketedItem(Socket socket)
    {
        // Check if socket is locked
        if (socket.IsLocked)
        {
            return new SocketRemovalResult
            {
                Success = false,
                Message = "Socket is locked and cannot be modified",
                RemovedItem = null
            };
        }
        
        // Check if socket is empty
        if (socket.Content == null)
        {
            return new SocketRemovalResult
            {
                Success = false,
                Message = "Socket is already empty",
                RemovedItem = null
            };
        }
        
        var removedItem = socket.Content;
        socket.Content = null;
        _logger.LogInformation("Removed {ItemName} from {SocketType} socket", removedItem.Name, socket.Type);
        
        return new SocketRemovalResult
        {
            Success = true,
            Message = $"Removed {removedItem.Name}",
            RemovedItem = removedItem
        };
    }
    
    /// <summary>
    /// Calculates the link bonus multiplier for linked sockets.
    /// </summary>
    /// <param name="linkSize">Number of sockets in the link group.</param>
    /// <returns>Bonus multiplier (1.0 = no bonus, 1.2 = 20% bonus).</returns>
    public double CalculateLinkBonus(int linkSize)
    {
        return linkSize switch
        {
            2 => 1.05,  // 5% bonus for 2-link
            3 => 1.10,  // 10% bonus for 3-link
            4 => 1.20,  // 20% bonus for 4-link
            5 => 1.30,  // 30% bonus for 5-link
            6 => 1.50,  // 50% bonus for 6-link
            _ => 1.0    // No bonus for unlinked or invalid
        };
    }
}

/// <summary>
/// Result of socket validation.
/// </summary>
public class SocketValidationResult
{
    /// <summary>Gets or sets a value indicating whether the socket operation is valid.</summary>
    public bool IsValid { get; set; }
    
    /// <summary>Gets or sets the error message if validation failed.</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Result of a socket operation.
/// </summary>
public class SocketOperationResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of removing a socketed item.
/// </summary>
public class SocketRemovalResult : SocketOperationResult
{
    /// <summary>Gets or sets the item that was removed from the socket.</summary>
    public ISocketable? RemovedItem { get; set; }
}
