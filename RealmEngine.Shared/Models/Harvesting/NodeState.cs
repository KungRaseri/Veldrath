namespace RealmEngine.Shared.Models.Harvesting;

/// <summary>
/// Represents the current state of a harvestable resource node based on health percentage.
/// </summary>
public enum NodeState
{
    /// <summary>
    /// Node is at 80-100% health - full yield, no penalties.
    /// </summary>
    Healthy,

    /// <summary>
    /// Node is at 40-79% health - normal yield, standard operation.
    /// </summary>
    Depleted,

    /// <summary>
    /// Node is at 10-39% health - 50% yield reduction, warning message displayed.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Node is at 0-9% health - cannot harvest, respawn required.
    /// </summary>
    Empty
}
