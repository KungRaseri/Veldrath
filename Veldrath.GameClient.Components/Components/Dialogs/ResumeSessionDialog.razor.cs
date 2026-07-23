using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Veldrath.GameClient.Components.Components.Dialogs;

/// <summary>
/// MudBlazor dialog that asks the player whether to resume an active game session
/// or choose a different character.
/// </summary>
/// <remarks>
/// The dialog returns <c>DialogResult.Ok(true)</c> when the user clicks "Resume"
/// and <c>DialogResult.Ok(false)</c> when they click "Choose Different Character".
/// </remarks>
public sealed partial class ResumeSessionDialog
{
    /// <summary>Gets or sets the MudDialog instance, provided by MudBlazor as a cascading parameter.</summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialogInstance { get; set; } = null!;

    /// <summary>Gets or sets the character name to display in the dialog.</summary>
    [Parameter]
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zone ID the character was last in,
    /// or <see langword="null"/> if the character was on the region map.
    /// </summary>
    [Parameter]
    public string? ZoneId { get; set; }

    /// <summary>User chose to resume the existing session.</summary>
    private void OnResume()
    {
        MudDialogInstance.Close(DialogResult.Ok(true));
    }

    /// <summary>User chose to pick a different character.</summary>
    private void OnChooseDifferent()
    {
        MudDialogInstance.Close(DialogResult.Ok(false));
    }
}
