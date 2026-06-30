using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Views;

/// <summary>
/// Panel that replaces the old tilemap control, showing location info,
/// exits, and entities as reactive lists rather than a 2D grid.
/// </summary>
public partial class ZoneLocationPanelView : ReactiveUserControl<ZoneLocationPanelViewModel>
{
    /// <summary>Initializes a new instance of <see cref="ZoneLocationPanelView"/>.</summary>
    public ZoneLocationPanelView()
    {
        InitializeComponent();
    }
}
