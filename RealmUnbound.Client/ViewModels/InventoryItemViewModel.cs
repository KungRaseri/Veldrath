using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a single item slot in the active character's inventory.</summary>
public sealed class InventoryItemViewModel : ViewModelBase
{
    private int _quantity;
    private int? _durability;

    /// <summary>Gets the item-reference slug (e.g. <c>"iron_sword"</c>).</summary>
    public string ItemRef { get; }

    /// <summary>Gets or sets the stack size for this inventory slot.</summary>
    public int Quantity
    {
        get => _quantity;
        set
        {
            this.RaiseAndSetIfChanged(ref _quantity, value);
            this.RaisePropertyChanged(nameof(QuantityText));
        }
    }

    /// <summary>Gets or sets the current durability (0–100), or <see langword="null"/> for stackable items without durability.</summary>
    public int? Durability
    {
        get => _durability;
        set
        {
            this.RaiseAndSetIfChanged(ref _durability, value);
            this.RaisePropertyChanged(nameof(DurabilityText));
            this.RaisePropertyChanged(nameof(HasDurability));
        }
    }

    /// <summary>Gets the display name derived from the item-reference slug.</summary>
    public string DisplayName => ItemRef
        .Split('/', ':')
        .Last()
        .Replace('-', ' ')
        .Replace('_', ' ');

    /// <summary>Gets a formatted quantity string, e.g. <c>"×3"</c>; empty for single items.</summary>
    public string QuantityText => _quantity > 1 ? $"×{_quantity}" : string.Empty;

    /// <summary>Gets a formatted durability string (e.g. <c>"75%"</c>), or an empty string when not applicable.</summary>
    public string DurabilityText => _durability.HasValue ? $"{_durability}%" : string.Empty;

    /// <summary>Gets a value indicating whether this item has durability tracking.</summary>
    public bool HasDurability => _durability.HasValue;

    /// <summary>Initializes a new instance of <see cref="InventoryItemViewModel"/>.</summary>
    /// <param name="itemRef">Item-reference slug.</param>
    /// <param name="quantity">Stack size.</param>
    /// <param name="durability">Current durability, or <see langword="null"/> for stackable items.</param>
    public InventoryItemViewModel(string itemRef, int quantity, int? durability)
    {
        ItemRef    = itemRef;
        _quantity  = quantity;
        _durability = durability;
    }
}
