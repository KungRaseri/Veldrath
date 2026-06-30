using Avalonia;
using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Rendering strategy for a tilemap. Implementations draw tile layers, entities,
/// overlays, and minimap using their own visual style (sprites, ASCII, etc.).
/// </summary>
public interface IMapRenderer
{
    /// <summary>Gets the fixed display size of a single tile in device pixels. Updated when <see cref="TileSize"/> is set.</summary>
    int DisplayTileSize { get; }

    /// <summary>Gets or sets the tile size in pixels used for rendering. Setting this value
    /// updates <see cref="DisplayTileSize"/> and may trigger metric recomputation in the renderer.</summary>
    double TileSize { get; set; }

    /// <summary>
    /// Renders one complete frame of the tilemap into <paramref name="context"/>.
    /// Called at ~30 fps by the hosting <see cref="Avalonia.Controls.Control"/>.
    /// </summary>
    /// <param name="context">The Avalonia drawing context for the current frame.</param>
    /// <param name="state">Snapshot of all data the renderer needs for this frame.</param>
    void Render(DrawingContext context, RenderState state);
}
