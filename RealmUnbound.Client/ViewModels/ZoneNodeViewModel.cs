using ReactiveUI;
using System.Reactive;

namespace Veldrath.Client.ViewModels;

/// <summary>
/// Display model for a single zone shown in the region map panel
/// and the zone panel's "Zones in this region" list.
/// Exposes a <see cref="TravelCommand"/> (non-null only for zones the character is not already in).
/// </summary>
public sealed class ZoneNodeViewModel
{
    /// <summary>Initializes a new instance of <see cref="ZoneNodeViewModel"/>.</summary>
    /// <param name="id">Zone slug identifier.</param>
    /// <param name="name">Display name of the zone.</param>
    /// <param name="type">Zone type classification (Town, Wilderness, Dungeon, Tutorial).</param>
    /// <param name="minLevel">Minimum recommended character level.</param>
    /// <param name="hasInn">Whether the zone has an inn for resting.</param>
    /// <param name="hasMerchant">Whether the zone has a merchant.</param>
    /// <param name="isCurrentZone">Whether the active character is currently in this zone.</param>
    /// <param name="onTravel">Async callback invoked when the player triggers travel to this zone. Pass <see langword="null"/> for the current zone.</param>
    public ZoneNodeViewModel(string id, string name, string type, int minLevel,
        bool hasInn, bool hasMerchant, bool isCurrentZone, Func<Task>? onTravel = null)
    {
        Id            = id;
        Name          = name;
        Type          = type;
        MinLevel      = minLevel;
        HasInn        = hasInn;
        HasMerchant   = hasMerchant;
        IsCurrentZone = isCurrentZone;
        TravelCommand = onTravel is not null
            ? ReactiveCommand.CreateFromTask(onTravel)
            : null;
    }

    /// <summary>Gets the slug identifier for this zone.</summary>
    public string Id { get; }

    /// <summary>Gets the display name of this zone.</summary>
    public string Name { get; }

    /// <summary>Gets the zone type classification (e.g. Town, Wilderness, Dungeon).</summary>
    public string Type { get; }

    /// <summary>Gets the minimum recommended character level for this zone.</summary>
    public int MinLevel { get; }

    /// <summary>Gets whether this zone has an inn for resting.</summary>
    public bool HasInn { get; }

    /// <summary>Gets whether this zone has a merchant.</summary>
    public bool HasMerchant { get; }

    /// <summary>Gets whether the active character is currently located in this zone.</summary>
    public bool IsCurrentZone { get; }

    /// <summary>Gets whether the character can travel to this zone (i.e. it is not the current zone).</summary>
    public bool CanTravel => !IsCurrentZone;

    /// <summary>Gets the command that initiates travel to this zone, or <see langword="null"/> if this is the current zone.</summary>
    public ReactiveCommand<Unit, Unit>? TravelCommand { get; }
}
