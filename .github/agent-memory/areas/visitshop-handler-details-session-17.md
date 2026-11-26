---
kind: area
date: undated
area: engine
title: "VisitShop Handler Details (session-17)"
---

## VisitShop Handler Details (session-17)

- `VisitShopHubCommand(Guid CharacterId, string ZoneId)` → `VisitShopHubResult`: `Success`, `Refusal`
  (`ShopRefusal`), `ErrorMessage`, `ZoneId`, `ZoneName`. In `Veldrath.Server/Features/Characters/VisitShopHubCommand.cs`.
- Validates the request in order: empty `ZoneId` → `ShopRefusal.InvalidRequest` with "Zone ID cannot be empty";
  unknown zone → `ShopRefusal.ZoneNotFound` with `"Zone '{zoneId}' not found"`; unknown character →
  `ShopRefusal.CharacterNotFound`.
- The merchant is then resolved by `ShopMerchantResolver.ResolveAsync`, which needs three facts to line up: the
  character's **stored** position (never the zone id the client sent), the zone's `HasMerchant` flag, and a
  `LocationNpc` at the character's current location whose archetype has `ArchetypeTraits.Shopkeeper == true`.
  A zone flag alone is not a merchant.
- Refusals from that step: `ShopRefusal.ZoneMismatch` → "You are not standing in that zone.";
  `ShopRefusal.NoMerchant` → "There is no one to trade with here."
- Success returns the handler's `ZoneId` / `ZoneName`. `GameHub.VisitShop(VisitShopHubRequest)` sends `ShopVisited`
  with `{ CharacterId, ZoneId, ZoneName }` **to the caller only** — never the zone group. The payload is an
  anonymous object; there is no C# `ShopVisitedPayload` record.
- `ShopVisited` only announces the merchant. The client must then ask for the stock, which the web
  `Session` does: `hub.on('ShopVisited')` sets `shopZone` / `showShop` and invokes `GetShopCatalog`.
  `GameHub.GetShopCatalog()` replies with `ShopCatalog` `{ ZoneId, Catalog: [{ Slug, Name, Rarity, Price }] }`,
  and `ShopCatalogPayload` in `web/client/src/lib/game/types.ts` is the web shape.
- Web request: `hub.invoke('VisitShop', { zoneId: this.zone.id })` from `session.svelte.ts`.
  `VisitShopHubRequest(string ZoneId)` is declared at the bottom of `Veldrath.Server/Hubs/GameHub.cs`.
- Handler tests have to seed all three resolver facts (stored location, `Zone.HasMerchant`, a shopkeeper NPC
  placed at that location); `Veldrath.Server.Tests/Features/ShopMerchantResolverTests.cs` is the reference.
