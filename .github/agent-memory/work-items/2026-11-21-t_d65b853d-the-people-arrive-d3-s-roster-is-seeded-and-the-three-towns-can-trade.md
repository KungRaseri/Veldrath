---
kind: work-item
date: 2026-11-21
area: content
card: t_d65b853d
title: "The people arrive: D3's roster is seeded, and the three towns can trade again"
---

### Session-D3-t_d65b853d (2026-11-21) — The people arrive: D3's roster is seeded, and the three towns can trade again

T1 left the shop honest and three towns unshoppable. The fix was content, not code: `LocationNpcsSeeder` now places all
thirteen rows of `docs/design/content/towns.md` §4, and `ArchetypeSeeder` carries an archetype for each.

- **Placement ids come from towns.md, not from the follow-up card.** The card listed `aldenmere-marketplace-cellar`
  for the cellar keeper; the approved table places `aldenmere-greycup-keeper` at `grey-cup`. The doc wins — it is the
  approved source and the card says so. Follow the roster table, not the prose summary of it.
- **Who trades is the "sells / teaches / refuses" column.** Eleven of the thirteen carry `Traits.Shopkeeper`; the two
  who do not are `wayfarer-elder` (teaches, no coin) and `harbour-master` (teaches the tide line, refuses berth).
  A shopkeeper archetype at a location the town does not flag is harmless: the resolver gates on the *zone's*
  `HasMerchant`, so crestfall's tavern keeper and the new carter trade from the town shelf wherever they stand (C6).
- **Every purse is inside its town's band, chosen there rather than caught by the test.** L0 150, L5 300/400/500,
  L10 900/800, L18 1600/2400/1800; `MerchantBandTests.Every_Seeded_Shopkeeper_Purse_Is_Inside_Its_Towns_Band` runs over
  the real `LocationNpcs` rows, so a new placement with a purse outside its town's tier reds the build.
- **New archetypes are named people.** `DisplayName` on a new archetype is the person (`Raekmeld`), because each is
  exactly one person; the three pre-existing generic archetypes keep role names and their three `DisplayName`
  overrides are still unseeded — towns.md §8 records that as owed with the wave B greetings.
- **The regression guard is the invariant, not a list.** `ShopMerchantResolverTests` resolves every `ZoneLocation`
  flagged `HasMerchant` in a non-dev merchant zone (six today) and fails when one has no shopkeeper or the zone has no
  shelf. The client offers its shop button from that same flag, so this is the test that keeps the button and the
  server agreeing.
- **Playground stays shopless on purpose.** `ZoneStockSeeder` has no `playground` rows and `LocationNpcsSeeder` places
  nobody there: the sandbox is `IsDevOnly`, its locations are fixtures, and inventing a roster would be inventing a
  settlement. Its shop refuses with `NoMerchant`, which is the documented decision.
