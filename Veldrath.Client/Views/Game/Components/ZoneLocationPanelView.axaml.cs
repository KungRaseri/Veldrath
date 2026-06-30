using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace Veldrath.Client.Views;

/// <summary>
/// Panel that replaces the old tilemap control, showing location info,
/// exits, and entities as reactive lists rather than a 2D grid.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class ZoneLocationPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="ZoneLocationPanelView"/>.</summary>
    public ZoneLocationPanelView()
    {
        InitializeComponent();
    }
}
