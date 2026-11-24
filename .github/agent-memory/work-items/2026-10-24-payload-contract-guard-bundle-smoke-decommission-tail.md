---
kind: work-item
date: 2026-10-24
area: map
title: "Payload contract guard, bundle smoke, decommission tail"
---

### Session-67 (2026-10-24) — Payload contract guard, bundle smoke, decommission tail

- **The hub wire is the contract, not the payload records.** `GameHub.cs` sends anonymous objects;
  `Veldrath.GameClient.Core/Payloads/*.cs` and `web/client/src/lib/game/types.ts` are two
  hand-maintained mirrors of them. `web/client/src/lib/game/payload-contract.test.ts` now fails when a
  mirrored payload's property sets diverge. Check GameHub first when it fires.
- **Wire shapes worth remembering** (they are *not* the old record shapes):
  `LocationEntered` → `{CharacterId, LocationSlug, LocationDisplayName, TypeKey, SpawnedEnemies[]}`;
  `CharacterRested` → `{CharacterId, LocationId, CurrentHealth, MaxHealth, CurrentMana, MaxMana, GoldRemaining}`;
  `AbilityUsed` → `{CharacterId, AbilityId, ManaCost, RemainingMana, HealthRestored}`;
  `AreaSearched` → `{CharacterId, RollValue, AnyFound, Discovered[]}`;
  `ShopCatalog` → `{ZoneId, Items, Catalog}` (Avalonia reads `Items`, web reads `Catalog`).
- **Desktop bundle gate**: `Veldrath.Client.Tests/HostedWeb/DesktopBundleSmokeTests.cs`
  (`[Trait("Category","Bundle")]`) serves the real `web/client/build-desktop` through
  `DesktopUiServer`. It no-ops locally without the bundle; `ci-client.yml` builds it first.
- **Server startup split**: `Infrastructure/CorsSetup.cs` and `Infrastructure/AuthSetup.cs`
  (`AddVeldrathIdentityAndAuth`) hold the CORS and Identity/JWT/OAuth registration; `Program.cs` is now
  ~520 lines. The rate limiter and health checks are still inline.
- **Docs gate**: `node scripts/check-markdown-links.mjs` (also run by `repo-hygiene.yml`) validates
  relative links, understands `path:line`, and skips `web/llms/` + the `wiki/` submodule. It found 18
  dead links after the plan prune — run it before touching docs.
- Full verification after this session: `dotnet build Realm.Full.slnx` + `dotnet test Realm.Full.slnx`
  = 4,630 tests, 0 failures; web `typecheck`/`lint`/`test` (22 passed, 1 skipped) all green.
