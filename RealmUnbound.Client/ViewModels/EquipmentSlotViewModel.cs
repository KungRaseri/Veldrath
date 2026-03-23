using Avalonia.Media.Imaging;
using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a single equipment slot on the active character.</summary>
public sealed class EquipmentSlotViewModel : ViewModelBase
{
    private string? _itemRef;
    private Bitmap? _icon;
    private Bitmap? _itemIcon;

    /// <summary>Canonical slot name used when communicating with the server (e.g. <c>MainHand</c>, <c>Head</c>).</summary>
    public string SlotName { get; }

    /// <summary>Human-readable display label for this slot (e.g. <c>Main Hand</c>).</summary>
    public string Label { get; }

    /// <summary>Slug of the item currently equipped in this slot, or <see langword="null"/> when empty.</summary>
    public string? ItemRef
    {
        get => _itemRef;
        set
        {
            this.RaiseAndSetIfChanged(ref _itemRef, value);
            this.RaisePropertyChanged(nameof(IsEmpty));
            this.RaisePropertyChanged(nameof(IsOccupied));
            this.RaisePropertyChanged(nameof(DisplayIcon));
        }
    }

    /// <summary>Generic slot-type icon displayed as a placeholder when the slot is empty.</summary>
    public Bitmap? Icon
    {
        get => _icon;
        set
        {
            this.RaiseAndSetIfChanged(ref _icon, value);
            this.RaisePropertyChanged(nameof(DisplayIcon));
        }
    }

    /// <summary>Icon for the specific item currently equipped in this slot, loaded from the asset store using <see cref="ItemRef"/> as the relative path.</summary>
    public Bitmap? ItemIcon
    {
        get => _itemIcon;
        set
        {
            this.RaiseAndSetIfChanged(ref _itemIcon, value);
            this.RaisePropertyChanged(nameof(DisplayIcon));
        }
    }

    /// <summary>
    /// The icon to render in the slot: the equipped item's icon when occupied, otherwise the slot-type placeholder.
    /// When the equipped item's icon cannot be resolved, falls back to the placeholder.
    /// </summary>
    public Bitmap? DisplayIcon => IsEmpty ? _icon : (_itemIcon ?? _icon);

    /// <summary><see langword="true"/> when no item is equipped in this slot.</summary>
    public bool IsEmpty => _itemRef is null;

    /// <summary><see langword="true"/> when an item is equipped in this slot.</summary>
    public bool IsOccupied => _itemRef is not null;

    /// <summary>Initializes a new instance of <see cref="EquipmentSlotViewModel"/>.</summary>
    /// <param name="slotName">Canonical slot name used by the server.</param>
    /// <param name="label">Human-readable display label.</param>
    public EquipmentSlotViewModel(string slotName, string label)
    {
        SlotName = slotName;
        Label    = label;
    }
}

