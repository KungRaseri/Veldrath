using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Complete rendering descriptor for a single tile index.
/// Bundles ASCII character, colours, and semantic metadata into one lookup result.
/// Both <see cref="AsciiMapRenderer"/> and <see cref="SpriteMapRenderer"/> can consume this.
/// </summary>
/// <param name="TileIndex">The spritesheet tile index (matches <see cref="RealmEngine.Shared.Models.TileIndex"/> constants).</param>
/// <param name="Name">Human-readable tile name (e.g. "Stone Wall", "Deep Water", "Pine Tree").</param>
/// <param name="Category">Broad tile classification.</param>
/// <param name="AsciiChar">Character to display in ASCII mode. Use <c>' '</c> for transparent/empty.</param>
/// <param name="Foreground">Foreground text colour in ASCII mode.</param>
/// <param name="Background">Optional background cell colour in ASCII mode. <see langword="null"/> means transparent.</param>
public readonly record struct TileDescriptor(
    int TileIndex,
    string Name,
    TileCategory Category,
    char AsciiChar,
    Color Foreground,
    Color? Background);
