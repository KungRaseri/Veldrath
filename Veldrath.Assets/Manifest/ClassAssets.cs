namespace Veldrath.Assets.Manifest;

/// <summary>
/// Path constants for character class badge icons (<c>"classes/"</c> prefix).
/// Each badge is a distinct image for the corresponding RPG class.
/// </summary>
public static class ClassAssets
{
    /// <summary>Warrior class badge.</summary>
    public const string Warrior = "classes/Badge_warrior.png";

    /// <summary>Mage class badge.</summary>
    public const string Mage = "classes/Badge_mage.png";

    /// <summary>Rogue class badge.</summary>
    public const string Rogue = "classes/Badge_rogue.png";

    /// <summary>Assassin class badge.</summary>
    public const string Assassin = "classes/Badge_assassin.png";

    /// <summary>Barbarian class badge.</summary>
    public const string Barbarian = "classes/Badge_barbarian.png";

    /// <summary>Hunter / Ranger class badge.</summary>
    public const string Hunter = "classes/Badge_hunter.png";

    /// <summary>Paladin class badge.</summary>
    public const string Paladin = "classes/Badge_paladin.png";

    /// <summary>Priest / Cleric class badge.</summary>
    public const string Priest = "classes/Badge_priest.png";

    /// <summary>Necromancer class badge.</summary>
    public const string Necromancer = "classes/Badge_necro.png";

    /// <summary>
    /// Returns the asset path for the given class name, or <see langword="null"/> if the name is not recognised.
    /// </summary>
    /// <param name="className">Display name of the class (case-sensitive, e.g. <c>"Warrior"</c>).</param>
    public static string? GetPath(string? className) => className switch
    {
        nameof(Warrior)     => Warrior,
        nameof(Mage)        => Mage,
        nameof(Rogue)       => Rogue,
        nameof(Assassin)    => Assassin,
        nameof(Barbarian)   => Barbarian,
        nameof(Hunter)      => Hunter,
        nameof(Paladin)     => Paladin,
        nameof(Priest)      => Priest,
        nameof(Necromancer) => Necromancer,
        _                   => null,
    };
}
