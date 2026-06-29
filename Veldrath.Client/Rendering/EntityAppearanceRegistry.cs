using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Maps entity sprite keys (and entity-type fallbacks) to their ASCII rendering appearance.
/// The <see cref="SpriteMapRenderer"/> does not use this for visuals but could use it for tooltips or labels.
/// </summary>
public static class EntityAppearanceRegistry
{
    private static readonly Dictionary<string, EntityAppearance> ByKey = new(StringComparer.OrdinalIgnoreCase);

    static EntityAppearanceRegistry()
    {
        // Entity-type defaults (fallback when no precise SpriteKey match exists)
        Register("player", '@', AsciiPalette.PlayerSelf);
        Register("enemy", 'E', AsciiPalette.Enemy);
        Register("npc", 'N', AsciiPalette.Npc);

        // Common enemies — sprite keys match EntitySpriteAssets.All entries
        Register("goblin-scout", 'g', AsciiPalette.Enemy);
        Register("goblin-warrior", 'G', AsciiPalette.Enemy);
        Register("skeleton", 'S', AsciiPalette.Enemy);
        Register("skeleton-archer", 'S', AsciiPalette.Enemy);
        Register("wolf", 'w', AsciiPalette.Enemy);
        Register("bear", 'B', AsciiPalette.Enemy);
        Register("giant-spider", 's', AsciiPalette.Enemy);
        Register("slime", 'o', AsciiPalette.Enemy);
        Register("bat", 'b', AsciiPalette.Enemy);
        Register("dragon", 'D', AsciiPalette.Enemy);
        Register("boss", 'M', AsciiPalette.Enemy);
    }

    /// <summary>
    /// Looks up the <see cref="EntityAppearance"/> for a given entity.
    /// Prefers an exact <paramref name="spriteKey"/> match, falling back to
    /// <paramref name="entityType"/> (<c>"player"</c>, <c>"enemy"</c>, <c>"npc"</c>).
    /// Returns a magenta <c>'?'</c> placeholder for completely unknown entities.
    /// </summary>
    /// <param name="spriteKey">The sprite key from <see cref="RenderEntity.SpriteKey"/>. May be <see langword="null"/>.</param>
    /// <param name="entityType">The entity type from <see cref="RenderEntity.EntityType"/>.</param>
    /// <returns>The rendering appearance for the entity.</returns>
    public static EntityAppearance Get(string? spriteKey, string entityType)
    {
        if (spriteKey is not null && ByKey.TryGetValue(spriteKey, out var bySprite))
            return bySprite;

        return ByKey.TryGetValue(entityType, out var byType)
            ? byType
            : new EntityAppearance('?', AsciiPalette.DebugUnknown);
    }

    /// <summary>
    /// Registers an entity appearance, keyed by sprite key or entity type.
    /// Can be called at startup to add custom entity appearances.
    /// </summary>
    /// <param name="key">The sprite key or entity type to register.</param>
    /// <param name="character">The ASCII character to display.</param>
    /// <param name="color">The foreground colour for the character.</param>
    public static void Register(string key, char character, Color color) =>
        ByKey[key] = new EntityAppearance(character, color);
}
