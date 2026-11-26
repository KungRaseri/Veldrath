---
kind: work-item
date: 2026-11-20
area: content
title: "The shop became a person: T1 lands the merchant band and the per-zone shelf"
---

### Session-109 (2026-11-20) — The shop became a person: T1 lands the merchant band and the per-zone shelf

The approved `merchants:` band in `game-context/budget.yaml` is now enforced by the shop path, and the per-zone stock
table exists. Four facts for the next session:

- **A merchant is a placed shopkeeper, not a zone flag.** `ShopMerchantResolver` requires the character's
  `CurrentZoneLocationSlug`, a `LocationNpc` at that exact zone+location, and an `ActorArchetype` with
  `Traits.Shopkeeper == true`; `Zone.HasMerchant` is still checked, and `RequestedZoneId != character.CurrentZoneId` is
  refused as `ZoneMismatch`. `VisitShop`, `GetShopCatalog`, `BuyItem` and `SellItem` all go through it and return a
  typed `ShopRefusal` next to the message ("There is no one to trade with here."). **Consequence to remember:** only
  crestfall has placements today (`LocationNpcsSeeder`), so aldenmere / tolvaren / skarhold refuse to trade until the
  D3 rosters are seeded, and `playground` refuses because it has no authored stock. That is the intended consequence,
  not a regression — the per-zone stock was authored anyway so the towns are ready when their people arrive.
- **Stock is `ZoneStockItems` in `ContentDbContext`** (migration `20261121004324_AddZoneStockItems`, unique on
  `(ZoneId, ItemSlug)`), authored by `RealmEngine.Data/Seeders/ZoneStockSeeder` — crestfall 9, aldenmere 12,
  tolvaren 11, skarhold 12 of the 24 purchasable items. The counts are load-bearing, not cosmetic: a shelf's
  dearest row is the ceiling that sizes its town's purse (`merchants.gold_basis` in `game-context/budget.yaml`), so
  `MerchantBandTests.Gold_Basis_Reproduces_Each_Towns_Band_From_Its_Authored_Shelf` now fails the moment a shelf
  edit re-bases a band. It is deliberately *not* a `ContentBase` family: a row is a
  zone↔item join with no slug or display name, so it is absent from `ContentEntityTypes`, the Foundry schema and the
  pack importer. Sell does not consult stock — a merchant buys back anything it is offered.
- **Pricing is one place:** `ShopPricing` applies `MerchantBands.PriceMarkupPercent` (100 = the band floor) to buy and
  `BuybackRatePercent` (50 = the band ceiling) to sell, and clamps sell below buy so even a 1-gold item cannot be
  bought and sold for the same coin. `MerchantBandTests` parses `budget.yaml` and fails when the C# copy drifts.
- **The blacksmith's purse moved 500 → 250 through the importer, not the seeder.** `ArchetypeSeeder` is insert-only, so
  `content/archetypes/village-blacksmith.yaml` is the delivery path: import an old (500) copy, then the real pack,
  reported `created 1` → `updated 1` → `unchanged 1` against Postgres 17, row `Version 2`, `TradeGold 250`. Change both
  in future (the seeder for fresh installs, the pack for existing rows) or a new database and an upgraded one disagree.
- **A shelf edit is a data migration — decided in `t_3231a2ea`, not deferred.** `ZoneStockItems` has no pack family and
  is not getting one. The reason is its shape, not its size: a row is a `(ZoneId, ItemSlug)` pair of soft string
  references with no slug (`ZoneStockItem.cs:22-31`), while the importer's whole update path is
  `FindBySlugAsync` on a `ContentBase` (`ContentPackImporter.cs:119`) and its child reconciliation hangs off a parent
  navigation (`ContentPackImporter.cs:275-306`); the `Zone` being named lives in `ApplicationDbContext`
  (`Veldrath.Server/Data/Entities/Zone.cs:7`, `ApplicationDbContext.cs:34`), a context an import never opens — one
  Postgres database, four contexts (`Program.cs:97-108`, `:240`) — so a pack family would need a `ContentBase` parent
  row (a shelf keyed by the zone id, not a phantom zone: `ZoneLocation` is the existing precedent of a content family
  carrying a loose `ZoneId`) purely to give the importer something to hang the list off, and it would still buy no
  validation, because the pack validator cannot resolve a gameplay zone id or read the live `Zone` table. Option 3 (a
  reconciling seeder) stays rejected: two writers to one table disagreeing in silence is the exact failure this
  decision prevents. **Ownership:** `Authors()` is the truth — the seeder materialises it on a database that has never
  been seeded, a migration carries the delta on one that has, nothing else writes these rows. Adding a row still needs
  no migration (the seeder inserts what is missing); removing one, or changing a `SortOrder`, does. Shape, verification
  query and the "who owns a shelf" statement: [`docs/content-packs.md`](../../../docs/content-packs.md); the standing rule
  for future sessions: [`engine-codebase.md`](../engine-codebase.md), "Content packs — the editable catalogue".

Test gotchas found on the way: a `ContentDbContext` sharing a SQLite connection after another context created tables
needs `Database.GetService<IRelationalDatabaseCreator>().CreateTables()` (EnsureCreated is a no-op); and a fixture that
seeds a context from its constructor must keep that context in a field — `using var db` there disposes it before the
first test runs.
