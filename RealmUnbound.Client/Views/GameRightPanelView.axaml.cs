using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Right side-panel component for the in-game HUD: online players, chat, and action log.</summary>
[ExcludeFromCodeCoverage]
public partial class GameRightPanelView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameRightPanelView"/>.</summary>
    public GameRightPanelView()
    {
        InitializeComponent();
    }
}
