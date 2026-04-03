using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Zone panel component: locations, enemy roster, combat HUD, and death overlay.</summary>
[ExcludeFromCodeCoverage]
public partial class GameZonePanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameZonePanelView"/>.</summary>
    public GameZonePanelView()
    {
        InitializeComponent();
    }
}
