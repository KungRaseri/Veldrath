---
kind: area
date: undated
area: engine
title: "P3 Stubs Status"
---

## P3 Stubs Status

> **P3 is closed, and this is the closed record — not current wiring.** Every item below was a fix to the
> **Avalonia desktop client**, which was deleted with its test project in session-68 (2026-10-25) — see
> [`work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md`](../work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md).
> The view models and their hub subscriptions do not exist; `ItemEquipped` survives only as a server
> broadcast consumed by `web/client/src/lib/game/session.svelte.ts`.
> Two pointers into live code: item 5's server half (`VisitShopHubCommand`, its handler and `GameHub.VisitShop`)
> still ships, with the web overlay in `web/client/src/lib/game/GameOverlays.svelte` as its client; the
> announcements half of item 6 survives as `Veldrath.Server/Features/Announcements`.
> [`engine-codebase.md`](../engine-codebase.md) points here for the full P3 status, so the list stays; the
> per-session dates are in [`session-log.md`](session-log.md).

1. ~~`MainMenuViewModel.SettingsCommand`~~ — FIXED session-3: navigates to `SettingsViewModel`
2. ~~`CharacterSelectViewModel.ServerUrl`~~ — FIXED session-5: delegates to `ClientSettings` singleton (injected)
3. ~~`SettingsViewModel` placeholder~~ — FIXED session-5: has real `ServerUrl` property + `ClientSettings` injection
4. ~~`ItemEquipped` hub subscription missing from `CharacterSelectViewModel`~~ — FIXED session-9: added `_itemEquippedSub` field, disposal, subscription, and `ItemEquippedPayload` record
5. ~~`DoVisitShopAsync` stub logging "Shop coming in M5."~~ — FIXED session-17: full hub bridge implemented (`VisitShopHubCommand` + handler + `GameHub.VisitShop` + client wiring)
6. ~~Server-offline UX: no feedback when server unreachable at launch~~ — FIXED session-19: server-status + announcement system (announcements survive as `Veldrath.Server/Features/Announcements` endpoints; the client-side status poller went with the client)
