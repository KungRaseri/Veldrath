using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace RealmUnbound.Client.Views;

/// <summary>Journal overlay modal: displays the character's active, completed, and failed quests.</summary>
[ExcludeFromCodeCoverage]
public partial class GameJournalOverlayView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="GameJournalOverlayView"/>.</summary>
    public GameJournalOverlayView()
    {
        InitializeComponent();
    }
}
