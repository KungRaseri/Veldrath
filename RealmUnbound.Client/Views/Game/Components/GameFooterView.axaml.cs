using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Footer component for the in-game HUD: navigation, ability hotbar, and context actions.</summary>
[ExcludeFromCodeCoverage]
public partial class GameFooterView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameFooterView"/>.</summary>
    public GameFooterView()
    {
        InitializeComponent();
    }
}
