namespace RealmUnbound.Assets.Manifest;

/// <summary>
/// Path constants for spell and skill icons (<c>"spells/"</c> prefix).
/// Icons are organised by colour, which corresponds to a magical tradition:
/// <list type="bullet">
///   <item><description><c>violet/</c> — Arcane tradition</description></item>
///   <item><description><c>yellow/</c> — Divine tradition</description></item>
///   <item><description><c>green/</c> — Primal tradition</description></item>
///   <item><description><c>red/</c>   — Occult tradition</description></item>
///   <item><description><c>blue/</c>  — Generic magic / utility</description></item>
///   <item><description><c>emerald/</c> — Healing / support</description></item>
///   <item><description><c>gray/</c>  — Passive / universal skills</description></item>
/// </list>
/// Use <see cref="IAssetStore.GetPaths"/> with <see cref="AssetCategory.Spells"/> to enumerate
/// all available spell icons.
/// </summary>
public static class SpellAssets
{
    // Arcane (violet)
    /// <summary>Arcane spell icon, violet palette, slot 01.</summary>
    public const string Arcane01 = "spells/violet/violet_01.PNG";

    // Divine (yellow)
    /// <summary>Divine spell icon, yellow palette, slot 01.</summary>
    public const string Divine01 = "spells/yellow/yellow_01.PNG";

    // Primal (green)
    /// <summary>Primal spell icon, green palette, slot 01.</summary>
    public const string Primal01 = "spells/green/green_01.PNG";

    // Occult (red)
    /// <summary>Occult spell icon, red palette, slot 01.</summary>
    public const string Occult01 = "spells/red/red_01.PNG";

    // Utility (blue)
    /// <summary>Utility spell icon, blue palette, slot 01.</summary>
    public const string Utility01 = "spells/blue/blue_01.png";

    // Healing (emerald)
    /// <summary>Healing spell icon, emerald palette, slot 01.</summary>
    public const string Heal01 = "spells/emerald/emerald_01.PNG";

    // Passive (gray)
    /// <summary>Passive skill icon, gray palette, slot 01.</summary>
    public const string Passive01 = "spells/gray/gray_01.png";

    /// <summary>Returns the subfolder for the given magical tradition (relative to <c>"spells/"</c>).</summary>
    /// <param name="tradition">Tradition name matching the server mapping: <c>"Arcane"</c>, <c>"Divine"</c>, <c>"Primal"</c>, or <c>"Occult"</c>.</param>
    /// <returns>The icon colour subfolder, or <c>"blue"</c> as a safe fallback.</returns>
    public static string FolderForTradition(string tradition) => tradition switch
    {
        "Arcane" => "violet",
        "Divine" => "yellow",
        "Primal" => "green",
        "Occult" => "red",
        _        => "blue",
    };
}
