using System.Reactive;
using ReactiveUI;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a single purchasable item in the merchant shop overlay.</summary>
public sealed class ShopItemViewModel : ViewModelBase
{
    /// <summary>Gets the item-reference slug (e.g. <c>"iron_sword"</c>).</summary>
    public string ItemRef { get; }

    /// <summary>Gets the human-readable display name of the item.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the gold cost to purchase this item from the merchant.</summary>
    public int BuyPrice { get; }

    /// <summary>Gets the gold the merchant pays when the player sells this item back.</summary>
    public int SellPrice { get; }

    /// <summary>Gets a formatted buy-price string (e.g. <c>"50g"</c>).</summary>
    public string BuyPriceText => $"{BuyPrice}g";

    /// <summary>Gets a formatted sell-price string (e.g. <c>"25g"</c>).</summary>
    public string SellPriceText => $"{SellPrice}g";

    /// <summary>Gets the command that purchases this item from the shop.</summary>
    public ReactiveCommand<Unit, Unit> BuyCommand { get; }

    /// <summary>Gets the command that sells this item to the merchant.</summary>
    public ReactiveCommand<Unit, Unit> SellCommand { get; }

    /// <summary>Initializes a new instance of <see cref="ShopItemViewModel"/>.</summary>
    /// <param name="itemRef">Item-reference slug.</param>
    /// <param name="displayName">Human-readable item name.</param>
    /// <param name="buyPrice">Gold cost to buy.</param>
    /// <param name="sellPrice">Gold received when selling.</param>
    /// <param name="onBuy">Async callback invoked when the player buys this item.</param>
    /// <param name="onSell">Async callback invoked when the player sells this item.</param>
    public ShopItemViewModel(string itemRef, string displayName, int buyPrice, int sellPrice,
        Func<Task>? onBuy = null, Func<Task>? onSell = null)
    {
        ItemRef     = itemRef;
        DisplayName = displayName;
        BuyPrice    = buyPrice;
        SellPrice   = sellPrice;
        BuyCommand  = ReactiveCommand.CreateFromTask(onBuy  ?? (() => Task.CompletedTask));
        SellCommand = ReactiveCommand.CreateFromTask(onSell ?? (() => Task.CompletedTask));
    }
}
