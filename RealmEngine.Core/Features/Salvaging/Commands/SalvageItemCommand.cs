using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Salvaging.Commands;

/// <summary>
/// Command to salvage an item for scrap materials.
/// Uses original crafting skill for yield bonus (+0.3% per level).
/// Requires crafting station. Returns scrap materials (3:1 refinement ratio).
/// </summary>
public class SalvageItemCommand : IRequest<SalvageItemResult>
{
    /// <summary>
    /// The character performing the salvage.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The item to salvage.
    /// </summary>
    public required Item Item { get; set; }

    /// <summary>
    /// The crafting station ID where salvaging is performed.
    /// </summary>
    public required string StationId { get; set; }
}

/// <summary>
/// Result of salvaging an item.
/// </summary>
public class SalvageItemResult
{
    /// <summary>
    /// Whether the salvage succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message describing what happened.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The scrap materials recovered from salvaging.
    /// Keys are scrap material names, values are quantities.
    /// </summary>
    public Dictionary<string, int> ScrapMaterials { get; set; } = new();

    /// <summary>
    /// The skill used for salvaging (e.g., "Blacksmithing" for weapons).
    /// </summary>
    public string? SkillUsed { get; set; }

    /// <summary>
    /// The skill-based yield rate applied (40-100%).
    /// </summary>
    public double YieldRate { get; set; }

    /// <summary>
    /// Whether the item was destroyed (always true on successful salvage).
    /// </summary>
    public bool ItemDestroyed { get; set; }
}
