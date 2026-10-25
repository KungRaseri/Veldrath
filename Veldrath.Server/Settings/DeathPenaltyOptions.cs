using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// The cost of dying for a normal (non-hardcore) character. Gold is taken first; experience is taken only when the
/// character carries less than <see cref="GoldFloor"/> gold. Never both — a broke player is not double-punished.
/// Values are validated at startup so a missing or nonsensical setting fails loudly instead of silently applying a
/// default in the middle of someone's death.
/// </summary>
public sealed class DeathPenaltyOptions
{
    /// <summary>Configuration section name for these options.</summary>
    public const string SectionName = "Death";

    /// <summary>Gets or sets the share of carried gold lost on death, in percent.</summary>
    [Range(0, 100)]
    public int GoldLossPercent { get; set; } = 10;

    /// <summary>Gets or sets the absolute gold cap per death, so one death never wipes out a fortune.</summary>
    [Range(0, 1_000_000)]
    public int GoldLossCap { get; set; } = 100;

    /// <summary>
    /// Gets or sets the gold a character must carry before gold is taken instead of experience. Below this floor the
    /// character is treated as broke, and the penalty moves to experience.
    /// </summary>
    [Range(0, 1_000_000)]
    public int GoldFloor { get; set; } = 10;

    /// <summary>Gets or sets the share of total experience lost when the character is below the gold floor, in percent.</summary>
    [Range(0, 100)]
    public int XpLossPercent { get; set; } = 5;
}
