using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Header component for the in-game HUD: compact character strip, zone info, and system controls.</summary>
[ExcludeFromCodeCoverage]
public partial class GameHeaderView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameHeaderView"/>.</summary>
    public GameHeaderView()
    {
        InitializeComponent();
    }
}
