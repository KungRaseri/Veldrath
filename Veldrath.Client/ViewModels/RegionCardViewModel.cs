using ReactiveUI;
using System.Reactive;

namespace Veldrath.Client.ViewModels;

/// <summary>
/// Display model for a region card shown on the world overview panel.
/// Exposes an <see cref="ExploreCommand"/> that drills into the region's zone list.
/// </summary>
public sealed class RegionCardViewModel
{
    /// <summary>Initializes a new instance of <see cref="RegionCardViewModel"/>.</summary>
    /// <param name="id">Region slug identifier.</param>
    /// <param name="name">Display name of the region.</param>
    /// <param name="type">Region type classification (Forest, Highland, Coastal, Volcanic).</param>
    /// <param name="minLevel">Minimum character level for zones in this region.</param>
    /// <param name="maxLevel">Maximum character level for zones in this region.</param>
    /// <param name="isCurrentRegion">Whether the active character's current zone is within this region.</param>
    /// <param name="onExplore">Async callback invoked when the player chooses to explore this region.</param>
    public RegionCardViewModel(string id, string name, string type,
        int minLevel, int maxLevel, bool isCurrentRegion, Func<Task>? onExplore = null)
    {
        Id              = id;
        Name            = name;
        Type            = type;
        MinLevel        = minLevel;
        MaxLevel        = maxLevel;
        IsCurrentRegion = isCurrentRegion;
        if (onExplore is not null)
            ExploreCommand = ReactiveCommand.CreateFromTask(onExplore);
    }

    /// <summary>Gets the slug identifier for this region.</summary>
    public string Id { get; }

    /// <summary>Gets the display name of this region.</summary>
    public string Name { get; }

    /// <summary>Gets the region type classification (Forest, Highland, Coastal, Volcanic).</summary>
    public string Type { get; }

    /// <summary>Gets the minimum character level for zones in this region.</summary>
    public int MinLevel { get; }

    /// <summary>Gets the maximum character level for zones in this region.</summary>
    public int MaxLevel { get; }

    /// <summary>Gets whether the active character's current zone is within this region.</summary>
    public bool IsCurrentRegion { get; }

    /// <summary>Gets the command that loads this region's zone details in the Region panel, or <see langword="null"/> when no explore callback was provided.</summary>
    public ReactiveCommand<Unit, Unit>? ExploreCommand { get; }
}
