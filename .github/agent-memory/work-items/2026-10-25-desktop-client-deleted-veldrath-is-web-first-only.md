---
kind: work-item
date: 2026-10-25
area: web
title: "Desktop client deleted, Veldrath is web-first only"
---

### Session-68 (2026-10-25) — Desktop client deleted, Veldrath is web-first only

- **There is no .NET client any more.** `Veldrath.Client`, `Veldrath.Client.Tests` (681 tests),
  `Veldrath.GameClient.Core` + tests, and `Veldrath.Auth` + tests are gone, along with
  `Veldrath.Client.slnx`, `Veldrath.slnx`, `versions/client.props`, `ci-client.yml`, and the `client`
  Codecov flag. Do not look for `GameViewModel`, `DesktopUiServer`, `GameWebView`, or the `/desktop`
  route: the SvelteKit app in `web/` is the only client.
- **Remaining .NET solutions:** `RealmEngine.slnx` (engine), `Veldrath.Server.slnx`,
  `Veldrath.Discord.slnx`, `Veldrath.Libraries.slnx` (assets/contracts/fonts/engine),
  `RealmForge.slnx`, `Realm.Full.slnx`. 7 test projects, 3,868 tests.
- **Better Auth only, but the server still owns email.** The account half of the bridge was deleted
  (`InternalAccountBridgeEndpoints`, `foundry-bridge.ts`, `/api/foundry-bridge/*`, the sync calls in
  `hooks.server.ts`, `/register`, `/account`). The **email half stays**: Better Auth relays
  verification/reset messages to `InternalEmailEndpoints` via
  [`email.ts`](../../../web/client/src/lib/server/email.ts) so there is one SMTP configuration and one
  set of templates, and `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN` remain in use. Do not move mail
  into the web app without a reason — that was tried and reverted.
- **Payload guard repointed at the wire.** `payload-contract.test.ts` now parses `GameHub.cs`
  directly (unioning multi-variant events like `CombatTurn`) instead of the deleted C# mirrors, and it
  caught two real mismatches during the deletion: `InventoryLoaded` and `ShopCatalog` were still
  sending Avalonia-only `Items`/`Equipped` projections, which are now removed.
- **JWT audience is still the string `Veldrath.Client`** in `appsettings*.json`, `docker-compose*.yml`,
  and `ci-web.yml`. Rename only as a coordinated change to both Coolify resources (a mismatch is an
  opaque `401`).
- Web build is `adapter-node` only now; `build:desktop`, `adapter-static`, and `GameHubOptions` were
  removed with the shell.
