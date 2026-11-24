---
kind: area
date: undated
area: engine
title: "ClientSettings (NEW session-5)"
---

## ClientSettings (NEW session-5)

> **Historical — the client this note documented was deleted in session-68 (2026-10-25).**
> It recorded that the Avalonia client's `ReactiveObject` `ClientSettings` carried `ServerBaseUrl` from its
> own `appsettings.json` (fallback `http://localhost:8080/`) into `CharacterSelectViewModel` and
> `SettingsViewModel` through DI. `Veldrath.Client/ClientSettings.cs`, `App.axaml.cs` and both constructor
> parameters do not exist, and no C# project reads a `ServerBaseUrl` key for the game client.
> `engine-codebase.md` no longer mentions `ClientSettings` either, so nothing depends on this note; the
> deletion record is
> [`work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md`](../work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md).

Where the client learns its endpoints now, read out of `web/client/src`:

- No settings singleton and no runtime server picker: the two endpoints are build-time constants declared in [`web/client/src/env.ts`](../../../web/client/src/env.ts) as `PUBLIC_API_URL` and `PUBLIC_HUB_URL`, inlined into the browser bundle.
- SignalR connects from [`web/client/src/lib/hub.ts`](../../../web/client/src/lib/hub.ts). It throws when `PUBLIC_HUB_URL` is empty, refuses an `http:` hub from an `https:` page (the browser blocks mixed content and SignalR reports only a negotiation failure), and takes its token from `accessTokenFactory` → `/api/game-token`.
- Server-to-server calls from SvelteKit resolve through `gameApiBaseUrl()` in [`web/client/src/lib/server/game-request.ts`](../../../web/client/src/lib/server/game-request.ts): `INTERNAL_BRIDGE_URL` first, then `PUBLIC_API_URL`.
- The only surviving `ServerBaseUrl` key belongs to the Discord bot — `Discord:ServerBaseUrl` in [`Veldrath.Discord/Settings/DiscordSettings.cs`](../../../Veldrath.Discord/Settings/DiscordSettings.cs).
