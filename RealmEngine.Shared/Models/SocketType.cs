namespace RealmEngine.Shared.Models;

/// <summary>
/// Types of sockets available for items.
/// Essences are NOT socketable - they are crafting materials only.
/// </summary>
public enum SocketType
{
    /// <summary>
    /// Physical gems providing attribute bonuses (Ruby: +STR, Sapphire: +INT, etc.).
    /// </summary>
    Gem,
    
    /// <summary>
    /// Inscribed runes providing proc effects (Fury Rune: chance to enrage, etc.).
    /// </summary>
    Rune,
    
    /// <summary>
    /// Energy crystals providing resource effects (Life Crystal: +max HP, +regen, etc.).
    /// </summary>
    Crystal,
    
    /// <summary>
    /// Skill orbs providing ability enhancements (Strike Orb: +skill damage, -cooldown, etc.).
    /// </summary>
    Orb
}
