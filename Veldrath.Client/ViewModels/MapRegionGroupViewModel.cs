namespace Veldrath.Client.ViewModels;

/// <summary>
/// Bounding-box data for a region cluster's background panel on the map canvas.
/// Populated by <see cref="MapViewModel"/> after the layout algorithm runs and used
/// to render a subtle backdrop that visually groups a region's header label and zone nodes.
/// </summary>
/// <param name="X">Canvas left position in pixels.</param>
/// <param name="Y">Canvas top position in pixels.</param>
/// <param name="Width">Panel width in pixels.</param>
/// <param name="Height">Panel height in pixels.</param>
public record MapRegionGroupViewModel(double X, double Y, double Width, double Height);
