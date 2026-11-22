using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Shop;

/// <summary>
/// Resolves the merchant a character is standing with from their own current zone and location.
/// </summary>
/// <remarks>
/// <para>
/// The character's stored position is authoritative, never the zone id the client sent: a client can ask about any
/// zone, and the answer must be about where the character actually is. A zone that does not match is refused before
/// any content is read.
/// </para>
/// <para>
/// Three facts have to line up, and each of them was previously ignored by the shop: the character is at a specific
/// location, the zone is flagged <see cref="Zone.HasMerchant"/>, and a <see cref="LocationNpc"/> placed at that very
/// location presents an archetype whose <see cref="RealmEngine.Data.Entities.ArchetypeTraits.Shopkeeper"/> flag is
/// set. The first merchant-shaped person wins, in placement order — two merchants at one location share one zone
/// stock list, so which of them serves the player cannot change the offer.
/// </para>
/// </remarks>
/// <param name="db">Application database, for the zone flag and the placements.</param>
/// <param name="content">Content catalog, for the archetype that carries the role traits and the purse.</param>
/// <param name="logger">Logger instance.</param>
public sealed class ShopMerchantResolver(
    ApplicationDbContext db,
    ContentDbContext content,
    ILogger<ShopMerchantResolver> logger)
    : IShopMerchantResolver
{
    /// <inheritdoc />
    public async Task<ShopMerchantResolution> ResolveAsync(
        Character character,
        string requestedZoneId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!string.IsNullOrWhiteSpace(requestedZoneId)
            && !string.Equals(requestedZoneId, character.CurrentZoneId, StringComparison.Ordinal))
        {
            logger.LogDebug(
                "Shop request for zone {RequestedZone} refused: character {CharacterId} is in {CurrentZone}",
                requestedZoneId, character.Id, character.CurrentZoneId);

            return ShopMerchantResolution.Refused(ShopRefusal.ZoneMismatch);
        }

        if (string.IsNullOrWhiteSpace(character.CurrentZoneLocationSlug))
            return ShopMerchantResolution.Refused(ShopRefusal.NoMerchant);

        var zone = await db.Zones
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == character.CurrentZoneId, cancellationToken);

        if (zone is null || !zone.HasMerchant)
        {
            logger.LogDebug(
                "Shop request refused: zone {ZoneId} has no merchant",
                character.CurrentZoneId);

            return ShopMerchantResolution.Refused(ShopRefusal.NoMerchant);
        }

        var placements = await db.LocationNpcs
            .AsNoTracking()
            .Where(npc => npc.ZoneId == character.CurrentZoneId
                          && npc.LocationSlug == character.CurrentZoneLocationSlug)
            .OrderBy(npc => npc.SortOrder)
            .ThenBy(npc => npc.ArchetypeSlug)
            .ToListAsync(cancellationToken);

        if (placements.Count == 0)
        {
            logger.LogDebug(
                "Shop request refused: nobody is placed at {ZoneId}/{LocationSlug}",
                character.CurrentZoneId, character.CurrentZoneLocationSlug);

            return ShopMerchantResolution.Refused(ShopRefusal.NoMerchant);
        }

        var slugs = placements
            .Select(npc => npc.ArchetypeSlug)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var archetypes = await content.ActorArchetypes
            .AsNoTracking()
            .Where(actor => slugs.Contains(actor.Slug) && actor.IsActive)
            .ToDictionaryAsync(actor => actor.Slug, StringComparer.Ordinal, cancellationToken);

        foreach (var placement in placements)
        {
            if (!archetypes.TryGetValue(placement.ArchetypeSlug, out var archetype)
                || archetype.Traits.Shopkeeper != true)
            {
                continue;
            }

            var merchant = new ShopMerchant(
                placement.ZoneId,
                placement.LocationSlug,
                archetype.Slug,
                placement.DisplayName ?? archetype.DisplayName ?? archetype.Slug,
                archetype.Stats.TradeGold ?? 0);

            logger.LogDebug(
                "Resolved merchant {ArchetypeSlug} at {ZoneId}/{LocationSlug} with purse {Gold}",
                merchant.ArchetypeSlug, merchant.ZoneId, merchant.LocationSlug, merchant.Gold);

            return ShopMerchantResolution.Found(merchant);
        }

        logger.LogDebug(
            "Shop request refused: nobody at {ZoneId}/{LocationSlug} is a shopkeeper",
            character.CurrentZoneId, character.CurrentZoneLocationSlug);

        return ShopMerchantResolution.Refused(ShopRefusal.NoMerchant);
    }
}
