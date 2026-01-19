using MediatR;
using RealmEngine.Shared.Models;
using Serilog;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Handler for previewing socket operations without committing changes.
/// </summary>
public class SocketPreviewHandler : IRequestHandler<SocketPreviewQuery, SocketPreviewResult>
{
    private readonly SocketService _socketService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketPreviewHandler"/> class.
    /// </summary>
    /// <param name="socketService">The socket service for validation logic.</param>
    public SocketPreviewHandler(SocketService socketService)
    {
        _socketService = socketService ?? throw new ArgumentNullException(nameof(socketService));
    }

    /// <summary>
    /// Handles the socket preview query.
    /// </summary>
    /// <param name="request">The preview query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview result.</returns>
    public Task<SocketPreviewResult> Handle(SocketPreviewQuery request, CancellationToken cancellationToken)
    {
        var result = new SocketPreviewResult();

        try
        {
            // Note: In production, you would load the actual equipment item from inventory
            // For preview purposes, we create a mock socket for validation
            var mockSocket = new Socket
            {
                Type = request.SocketableItem.SocketType,
                IsLocked = false,
                Content = null
            };

            // Validate the operation
            var validation = _socketService.ValidateSocketing(mockSocket, request.SocketableItem);
            result.CanSocket = validation.IsValid;
            result.Message = validation.IsValid 
                ? $"Can socket {request.SocketableItem.Name}" 
                : validation.ErrorMessage;

            if (validation.IsValid)
            {
                // Show what traits would be applied
                result.TraitsToApply = new Dictionary<string, TraitValue>(request.SocketableItem.Traits);

                // Convert traits to display format
                foreach (var trait in request.SocketableItem.Traits)
                {
                    var value = trait.Value.Type == TraitType.Number 
                        ? trait.Value.AsDouble() 
                        : 0.0;
                    var isPercentage = value < 1.0 && value > 0; // Values < 1 are percentages
                    
                    result.StatBonuses.Add(new StatBonusDto
                    {
                        StatName = trait.Key,
                        Value = value,
                        IsPercentage = isPercentage,
                        DisplayText = isPercentage 
                            ? $"+{value * 100:F1}% {trait.Key}" 
                            : $"+{value} {trait.Key}"
                    });
                }

                // Check for link activation (simplified - in production would check actual socket configuration)
                // This would require checking adjacent sockets to see if they form a link group
                result.WouldActivateLink = false; // Default to false without full item context
                result.LinkBonusMultiplier = 1.0;
                result.LinkSize = 0;

                // Add helpful warnings
                // Note: Prismatic/universal socket type would be checked here if implemented
                // if (request.SocketableItem.SocketType == SocketType.Universal)
                // {
                //     result.Warnings.Add("This is a universal socket - can accept any socketable type");
                // }

                Log.Debug("Socket preview for {ItemName} at index {Index}: {CanSocket}",
                    request.SocketableItem.Name, request.SocketIndex, result.CanSocket);
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during socket preview");
            result.CanSocket = false;
            result.Message = $"Preview failed: {ex.Message}";
            return Task.FromResult(result);
        }
    }
}
