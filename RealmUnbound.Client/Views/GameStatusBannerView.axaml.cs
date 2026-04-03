using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Status message banner: full-width top-anchored notification with optional dismiss.</summary>
[ExcludeFromCodeCoverage]
public partial class GameStatusBannerView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameStatusBannerView"/>.</summary>
    public GameStatusBannerView()
    {
        InitializeComponent();
    }
}
