using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace Veldrath.Client.Views;

/// <summary>Inventory overlay modal: item list with equip/drop actions.</summary>
[ExcludeFromCodeCoverage]
public partial class GameInventoryOverlayView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameInventoryOverlayView"/>.</summary>
    public GameInventoryOverlayView()
    {
        InitializeComponent();
    }
}
