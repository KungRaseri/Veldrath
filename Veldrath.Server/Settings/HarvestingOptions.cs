using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// Harvesting tunables, bound from the <c>Harvesting</c> configuration section.
/// </summary>
/// <remarks>
/// Regrowth is lazy on purpose: a node comes back the next time somebody looks at the location, instead of a
/// background sweep walking every node in the world. The only thing that has to be true is that an empty node
/// does not stay empty forever — nothing called the engine's <c>RespawnNode</c>, so a harvested-out node was
/// permanently dead.
/// </remarks>
public sealed class HarvestingOptions
{
    /// <summary>The configuration section this class binds to.</summary>
    public const string SectionName = "Harvesting";

    /// <summary>
    /// Gets how long a fully depleted node stays empty before it regrows, in minutes.
    /// </summary>
    [Range(1, 24 * 60)]
    public int RegrowMinutes { get; init; } = 30;
}
