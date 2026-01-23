using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Death.Commands;

/// <summary>
/// Command to handle player death with appropriate penalties.
/// </summary>
public record HandlePlayerDeathCommand : IRequest<HandlePlayerDeathResult>
{
    /// <summary>Gets the player character who died.</summary>
    public required Character Player { get; init; }
    /// <summary>Gets the location where the player died.</summary>
    public required string DeathLocation { get; init; }
    /// <summary>Gets the enemy that killed the player, if any.</summary>
    public Enemy? Killer { get; init; }
}

/// <summary>
/// Result of player death handling.
/// </summary>
public record HandlePlayerDeathResult
{
    /// <summary>Gets a value indicating whether this was a permadeath.</summary>
    public required bool IsPermadeath { get; init; }
    /// <summary>Gets a value indicating whether the save was deleted due to permadeath.</summary>
    public required bool SaveDeleted { get; init; }
    /// <summary>Gets the items dropped by the player upon death.</summary>
    public List<Item> DroppedItems { get; init; } = new();
    /// <summary>Gets the amount of gold lost upon death.</summary>
    public int GoldLost { get; init; }
    /// <summary>Gets the amount of experience lost upon death.</summary>
    public int XPLost { get; init; }
    /// <summary>Gets the hall of fame entry ID if permadeath occurred.</summary>
    public string? HallOfFameId { get; init; }
    /// <summary>Gets the hall of fame entry if permadeath occurred (for display in Godot).</summary>
    public HallOfFameEntry? HallOfFameEntry { get; init; }
}