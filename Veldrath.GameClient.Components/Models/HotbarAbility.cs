namespace Veldrath.GameClient.Components.Models;

/// <summary>
/// Represents a single ability quick-slot on the hotbar action bar.
/// Mirrors the desktop's <c>HotbarSlotViewModel</c> pattern adapted for Blazor binding.
/// </summary>
/// <param name="SlotNumber">The 1-based slot position (1–10).</param>
/// <param name="KeyBind">The keyboard key bound to this slot (e.g. "1", "2", …, "0").</param>
/// <param name="AbilityId">The ability slug, or <c>null</c> if the slot is empty.</param>
/// <param name="Name">The display name of the ability, or <c>null</c> if empty.</param>
/// <param name="IconClass">CSS class for the ability icon, or <c>null</c> for empty slots.</param>
/// <param name="CooldownRemaining">Cooldown seconds remaining, or <c>0</c> if ready.</param>
/// <param name="IsAvailable"><see langword="true"/> when the ability can be used (no cooldown, has mana, etc.).</param>
public sealed record HotbarAbility(
    int SlotNumber,
    string KeyBind,
    string? AbilityId,
    string? Name,
    string? IconClass,
    int CooldownRemaining,
    bool IsAvailable)
{
    /// <summary>Gets whether this slot has no ability assigned.</summary>
    public bool IsEmpty => AbilityId is null;

    /// <summary>Gets the CSS classes for the hotbar slot button.</summary>
    public string CssClass
    {
        get
        {
            if (IsEmpty) return "hotbar-slot-empty";
            if (!IsAvailable) return "hotbar-slot-cooldown";
            return "hotbar-slot-ready";
        }
    }
}
