---
kind: area
date: undated
area: engine
title: "VisitShop Handler Details (session-17)"
---

## VisitShop Handler Details (session-17)

- Uses `IZoneRepository.GetByIdAsync(zoneId)` — validates zone exists + `HasMerchant == true`
- ZoneId empty string → fails with "Zone ID cannot be empty"
- Zone not found → fails with `"Zone '{zoneId}' not found"`
- Zone `HasMerchant == false` → fails with `"Zone '{name}' has no merchant"`
- Returns `{ ZoneId, ZoneName }` on success
- Broadcasts `ShopVisited` payload: `{ CharacterId, ZoneId, ZoneName }` — **to Caller ONLY** (not zone group)
- `ShopVisitedPayload(Guid CharacterId, string ZoneId, string ZoneName)` internal record in `CharacterSelectViewModel.cs`
- 18th subscription: `_shopVisitedSub` field + disposal + `"ShopVisited"` subscription in `DoSelectAsync`
- Client callback: `GameViewModel.OnShopVisited(string zoneId, string zoneName)` → `AppendLog("Welcome to the shop at {zoneName}!")`
- Client sends: `SendCommandAsync<object>("VisitShop", new { ZoneId = _currentZoneId })`
- `VisitShopHubRequest(string ZoneId)` DTO record at bottom of `GameHub.cs`
- Zone with `HasMerchant = true` for handler tests: `"fenwick-crossing"` (from seed data)
- Zone with `HasMerchant = false` for handler tests: `"greenveil-paths"`
