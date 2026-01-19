using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing;

/// <summary>
/// Godot-optimized DTO for socket information.
/// Uses flat lists instead of dictionaries for easier GDScript interop.
/// </summary>
public class SocketInfoDto
{
    /// <summary>Gets or sets the equipment item ID.</summary>
    public string ItemId { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the equipment item name.</summary>
    public string ItemName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets all sockets in the item (flat list with indices).</summary>
    public List<SocketSlotDto> Sockets { get; set; } = new();
    
    /// <summary>Gets or sets the total number of sockets.</summary>
    public int TotalSockets { get; set; }
    
    /// <summary>Gets or sets the number of filled sockets.</summary>
    public int FilledSockets { get; set; }
    
    /// <summary>Gets or sets the number of empty sockets.</summary>
    public int EmptySockets { get; set; }
    
    /// <summary>Gets or sets information about linked socket groups.</summary>
    public List<LinkGroupDto> LinkGroups { get; set; } = new();
    
    /// <summary>Gets or sets the total stat bonuses from all socketed items.</summary>
    public List<StatBonusDto> TotalBonuses { get; set; } = new();
}

/// <summary>
/// Information about a single socket slot (Godot-friendly).
/// </summary>
public class SocketSlotDto
{
    /// <summary>Gets or sets the socket index (0-based).</summary>
    public int Index { get; set; }
    
    /// <summary>Gets or sets the socket type name for display.</summary>
    public string TypeName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the socket type enum value.</summary>
    public SocketType Type { get; set; }
    
    /// <summary>Gets or sets a value indicating whether the socket is empty.</summary>
    public bool IsEmpty { get; set; }
    
    /// <summary>Gets or sets a value indicating whether the socket is locked.</summary>
    public bool IsLocked { get; set; }
    
    /// <summary>Gets or sets the link group ID (-1 if unlinked).</summary>
    public int LinkGroup { get; set; }
    
    /// <summary>Gets or sets the socketable item information if filled.</summary>
    public SocketableItemDto? Content { get; set; }
    
    /// <summary>Gets or sets the icon path for UI display.</summary>
    public string IconPath { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the tooltip text for this socket.</summary>
    public string TooltipText { get; set; } = string.Empty;
}

/// <summary>
/// Information about a socketable item (Gem, Rune, Crystal, Orb).
/// </summary>
public class SocketableItemDto
{
    /// <summary>Gets or sets the item ID.</summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the socket type this item fits into.</summary>
    public SocketType SocketType { get; set; }
    
    /// <summary>Gets or sets the category (e.g., "red", "offensive", "life").</summary>
    public string? Category { get; set; }
    
    /// <summary>Gets or sets the rarity level.</summary>
    public ItemRarity Rarity { get; set; }
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the stat bonuses provided by this item.</summary>
    public List<StatBonusDto> Bonuses { get; set; } = new();
    
    /// <summary>Gets or sets the icon path.</summary>
    public string IconPath { get; set; } = string.Empty;
}

/// <summary>
/// Stat bonus information for UI display.
/// </summary>
public class StatBonusDto
{
    /// <summary>Gets or sets the stat name (e.g., "Strength", "MaxHealth").</summary>
    public string StatName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the bonus value.</summary>
    public double Value { get; set; }
    
    /// <summary>Gets or sets a value indicating whether this is a percentage bonus.</summary>
    public bool IsPercentage { get; set; }
    
    /// <summary>Gets or sets the display text (e.g., "+15 Strength", "+10% Crit Chance").</summary>
    public string DisplayText { get; set; } = string.Empty;
}

/// <summary>
/// Information about a linked socket group.
/// </summary>
public class LinkGroupDto
{
    /// <summary>Gets or sets the link group identifier.</summary>
    public int LinkGroupId { get; set; }
    
    /// <summary>Gets or sets the socket indices in this link.</summary>
    public List<int> SocketIndices { get; set; } = new();
    
    /// <summary>Gets or sets the number of sockets in this link.</summary>
    public int LinkSize { get; set; }
    
    /// <summary>Gets or sets a value indicating whether this link is fully filled.</summary>
    public bool IsActive { get; set; }
    
    /// <summary>Gets or sets the bonus multiplier (1.0 = no bonus, 1.2 = 20% bonus).</summary>
    public double BonusMultiplier { get; set; }
    
    /// <summary>Gets or sets the description of the link bonus.</summary>
    public string BonusDescription { get; set; } = string.Empty;
}
