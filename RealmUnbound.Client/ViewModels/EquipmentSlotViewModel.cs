using Avalonia.Media.Imaging;
using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a single equipment slot on the active character.</summary>
public sealed class EquipmentSlotViewModel : ViewModelBase
{
    private string? _itemRef;
    private Bitmap? _icon;

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
        }
    }

    /// <summary>Generic slot-type icon displayed as a placeholder (weapon, armour, or accessory icon).</summary>
    public Bitmap? Icon
    {
        get => _icon;
        set => this.RaiseAndSetIfChanged(ref _icon, value);
    }

    /// <summary><see langword="true"/> when no item is equipped in this slot.</summary>
    public bool IsEmpty => _itemRef is null;

    /// <summary>Initializes a new instance of <see cref="EquipmentSlotViewModel"/>.</summary>
    /// <param name="slotName">Canonical slot name used by the server.</param>
    /// <param name="label">Human-readable display label.</param>
    public EquipmentSlotViewModel(string slotName, string label)
    {
        SlotName = slotName;
        Label    = label;
    }
}
