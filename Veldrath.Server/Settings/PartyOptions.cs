using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// Party tuning: how large a party may grow and how long an unanswered invite survives.
/// </summary>
/// <remarks>
/// Bound from the <c>Party</c> configuration section with <c>ValidateOnStart</c>, so a nonsensical value fails the host
/// at startup rather than silently falling back — the same rule the world settings follow.
/// </remarks>
public sealed class PartyOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Party";

    /// <summary>Gets or sets the maximum number of characters in one party.</summary>
    [Range(1, 40)]
    public int MaxSize { get; set; } = 5;

    /// <summary>Gets or sets how long an unanswered invite stays valid, in minutes.</summary>
    [Range(1, 1440)]
    public int InviteExpiryMinutes { get; set; } = 5;

    /// <summary>Gets the invite lifetime as a duration.</summary>
    public TimeSpan InviteExpiry => TimeSpan.FromMinutes(InviteExpiryMinutes);
}
