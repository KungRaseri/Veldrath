using Avalonia;
using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Rendering strategy for a tilemap. Implementations draw tile layers, entities,
/// overlays, and minimap using their own visual style (sprites, ASCII, etc.).
/// </summary>
public interface IMapRenderer
{
    /// <summary>Fixed display size of a single tile in device pixels.</summary>
    int DisplayTileSize { get; }

    /// <summary>
    /// Renders one complete frame of the tilemap into <paramref name="context"/>.
    /// Called at ~30 fps by the hosting <see cref="Avalonia.Controls.Control"/>.
    /// </summary>
    /// <param name="context">The Avalonia drawing context for the current frame.</param>
    /// <param name="state">Snapshot of all data the renderer needs for this frame.</param>
    void Render(DrawingContext context, RenderState state);
}
