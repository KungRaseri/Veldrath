namespace Veldrath.Server.Features.Shop;

/// <summary>
/// Why a shop request was refused, so the client can render a reason instead of parsing prose.
/// </summary>
/// <remarks>
/// Every shop handler reports a refusal code beside its message. A client that only understands the message keeps
/// working; one that wants to style, localise or act on the reason ("there is nobody here" versus "you cannot afford
/// it") has a stable value to switch on instead of matching strings.
/// </remarks>
public enum ShopRefusal
{
    /// <summary>No refusal — the request succeeded.</summary>
    None = 0,

    /// <summary>The request itself was malformed, e.g. an empty item reference.</summary>
    InvalidRequest,

    /// <summary>The character does not exist, or has been deleted.</summary>
    CharacterNotFound,

    /// <summary>The named zone is not the zone the character is standing in.</summary>
    ZoneMismatch,

    /// <summary>The named zone does not exist.</summary>
    ZoneNotFound,

    /// <summary>No living merchant stands with the character, so there is no one to trade with.</summary>
    NoMerchant,

    /// <summary>The item named by the request is not in the item catalog at all.</summary>
    ItemNotFound,

    /// <summary>The merchant is real, but does not offer this item in this zone.</summary>
    ItemNotInStock,

    /// <summary>The character cannot pay the purchase price.</summary>
    InsufficientGold,

    /// <summary>The character does not carry the item being sold.</summary>
    ItemNotHeld,

    /// <summary>The merchant's purse cannot cover this single buyback.</summary>
    MerchantCannotAfford,
}
