using Microsoft.AspNetCore.Components;

namespace Veldrath.GameClient.Components.Components.Layout;

/// <summary>
/// Layout used for game pages (routes under <c>/Game/</c>).  Applies a distinct CSS Grid
/// layout that bypasses the main site chrome so the game screen can use its own full-viewport
/// presentation.
/// </summary>
public partial class GameLayout : LayoutComponentBase
{
}
