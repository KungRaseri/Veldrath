using MudBlazor;

namespace Veldrath.GameClient.Components.Models;

/// <summary>A labelled value rendered by <c>StatRow</c> and <c>StatTable</c>.</summary>
/// <param name="Label">Row label.</param>
/// <param name="Value">Display value.</param>
/// <param name="Icon">Optional Material icon key.</param>
/// <param name="Color">Optional value colour.</param>
public sealed record StatEntry(
    string Label,
    string Value,
    string? Icon = null,
    Color? Color = null);

/// <summary>A selectable row rendered by <c>EntityList</c>.</summary>
/// <param name="Id">Stable identifier used for selection and testids.</param>
/// <param name="Name">Primary label.</param>
/// <param name="Subtitle">Optional secondary text.</param>
/// <param name="Icon">Optional Material icon key.</param>
/// <param name="Badge">Optional trailing badge text.</param>
/// <param name="IsSelected">Whether the row is currently selected.</param>
/// <param name="IsDisabled">Whether the row cannot be selected.</param>
public sealed record EntityListItem(
    string Id,
    string Name,
    string? Subtitle = null,
    string? Icon = null,
    string? Badge = null,
    bool IsSelected = false,
    bool IsDisabled = false);

/// <summary>A row rendered by <c>ItemTable</c>.</summary>
/// <param name="Id">Stable identifier used for selection and testids.</param>
/// <param name="Name">Item display name.</param>
/// <param name="Quantity">Stack quantity.</param>
/// <param name="Type">Item category or slot.</param>
/// <param name="Value">Formatted value, such as gold or weight.</param>
/// <param name="Icon">Optional Material icon key.</param>
/// <param name="IsEquipped">Whether the item is currently equipped.</param>
public sealed record ItemTableEntry(
    string Id,
    string Name,
    int Quantity = 1,
    string? Type = null,
    string? Value = null,
    string? Icon = null,
    bool IsEquipped = false);
