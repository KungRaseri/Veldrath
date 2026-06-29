using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Rendering appearance for an entity type in ASCII mode.
/// Maps a <see cref="RenderEntity.SpriteKey"/> or <see cref="RenderEntity.EntityType"/> to visual properties.
/// </summary>
/// <param name="Character">Display character in ASCII mode.</param>
/// <param name="Color">Foreground colour for the character.</param>
public readonly record struct EntityAppearance(char Character, Color Color);
