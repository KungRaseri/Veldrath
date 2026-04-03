using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Left side-panel component for the in-game HUD: gold, attributes, and equipment slots.</summary>
[ExcludeFromCodeCoverage]
public partial class GameLeftPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameLeftPanelView"/>.</summary>
    public GameLeftPanelView()
    {
        InitializeComponent();
    }
}
