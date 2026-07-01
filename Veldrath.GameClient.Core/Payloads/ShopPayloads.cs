namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character visits a shop or merchant.
/// </summary>
/// <param name="ZoneId">The zone identifier where the shop is located.</param>
/// <param name="ZoneName">The display name of the shop or zone.</param>
public sealed record ShopVisitedPayload(string ZoneId, string ZoneName);
