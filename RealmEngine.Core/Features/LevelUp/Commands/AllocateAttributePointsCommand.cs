using MediatR;
using System.Collections.Generic;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Command to allocate attribute points to character attributes.
/// </summary>
public class AllocateAttributePointsCommand : IRequest<AllocateAttributePointsResult>
{
    /// <summary>
    /// The name of the character allocating points.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of attribute allocations (attribute name -> points to allocate).
    /// Example: { "Strength": 2, "Intelligence": 3 }
    /// </summary>
    public Dictionary<string, int> AttributeAllocations { get; set; } = new();
}

/// <summary>
/// Result of allocating attribute points.
/// </summary>
public class AllocateAttributePointsResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Points spent in this operation.
    /// </summary>
    public int PointsSpent { get; set; }

    /// <summary>
    /// Remaining unallocated attribute points.
    /// </summary>
    public int RemainingPoints { get; set; }

    /// <summary>
    /// New attribute values after allocation.
    /// </summary>
    public Dictionary<string, int> NewAttributeValues { get; set; } = new();
}
