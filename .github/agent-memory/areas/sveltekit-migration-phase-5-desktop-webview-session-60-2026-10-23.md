---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23)"
---

## SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23)

> **Closed record: the desktop host this note describes no longer exists.** `Veldrath.Client`, `Veldrath.Client.Tests`,
> `HostedWeb/DesktopUiServer`, the `/desktop` route, `VELDRATH_DESKTOP_UI`, `web/client/build-desktop`,
> `npm run build:desktop` and the `adapter-static` desktop mode were all deleted with the Avalonia client in
> session-68 (2026-10-25) — see
> [`work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md`](../work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md).
> Checked against `development` when this banner was written: `web/client/package.json` builds with
> `@sveltejs/adapter-node` only, `vite.config.ts` has no desktop mode, there is no `build-desktop` directory and no
> `/desktop` route under `web/client/src/routes/`.
> One artefact named here survives where it always lived: `GameScreen.svelte` is now
> [`web/client/src/lib/game/GameScreen.svelte`](../../../web/client/src/lib/game/GameScreen.svelte) and is rendered by
> the single `/play/[id]` route — the second route it was shared with went with the shell.
> Everything below is kept as the record of how that shell worked; do not read any path in it as current.

**Architecture (complete on build/test path)**
- `Veldrath.Client/HostedWeb/DesktopUiServer` replaces `HostedGameServer` (deleted). It:
  - binds Kestrel to `127.0.0.1:0` and serves the static Svelte build with
    `UseDefaultFiles`/`UseStaticFiles` + `MapFallbackToFile("index.html")`;
  - exposes `/health`;
  - proxies `/api/{**path}` and `/hubs/{**path}` (HTTP + WebSocket) to the remote server,
    injecting `Authorization: Bearer <TokenStore token>` server-side;
  - refreshes expiring tokens through `IAuthService.RefreshAsync` supplied by App DI.
- UI path resolution: `VELDRATH_DESKTOP_UI` → `SvelteApp` next to the executable →
  `web/client/build-desktop` during development. Throws a clear error when missing.
- `Veldrath.Client.csproj` copies `web/client/build-desktop/**` to output as `SvelteApp/`
  when it exists, and no longer references `Veldrath.GameClient.Components` or
  `Veldrath.Auth.Blazor`. `Microsoft.AspNetCore.App` FrameworkReference stays.
- `GameViewModel.WebViewUrl` is now `{BaseUrl}/desktop`; native Avalonia fallback unchanged.

**Svelte desktop build**
- `vite.config.ts` selects `@sveltejs/adapter-static@3` when mode is `desktop`
  (`vite build --mode desktop`, script `npm run build:desktop`), output
  `web/client/build-desktop`, fallback `index.html`. adapter-static v4 requires SvelteKit 3.
- No prerender: pure SPA fallback. Root layout uses optional chaining
  (`data?.user`, `data?.staff?.foundry`) because the fallback shell has no server load data.
  `$app/state` `page.url` hides the public header/footer on `/desktop`.
- `/desktop` (`ssr = false`) loads `/api/characters` through the same-origin proxy, then
  starts `GameSession` with `{ hubUrl: '/hubs/game' }`. `GameHubOptions.hubUrl` overrides the
  env URL and omits `accessTokenFactory` so the proxy supplies auth.
- `GameScreen.svelte` extracted from `/play/[id]` and shared by both routes.

**Gotchas**
- Do NOT run `vite build` while `vite dev` is running: both use `.svelte-kit/output`, which
  produced a `.fuse_hidden*` chunk, an hour-long stuck prerender, and
  `ENOENT ... .fuse_hidden` during the static adapter walk. Stop the dev server first.
- The static SPA fallback serves `/desktop` at any path; client-side routing handles it.
- WebView2 execution cannot be tested on Linux; `DesktopUiServer` integration tests cover
  static hosting, fallback routing, token-injecting proxy, 401, and shutdown on the build host.

**Verification**
- `npm run build:desktop` writes `build-desktop/index.html`.
- `Veldrath.Client.Tests`: **677 passing**, including 7 `DesktopUiServerIntegrationTests`.
- Chromium smoke of the static bundle with a mocked `/api/characters`: character list renders,
  public nav hidden, clicking a character renders the game shell; hub negotiation fails only
  because the smoke server intentionally has no `/hubs` proxy.
- Remaining release check: Windows WebView2 runtime smoke with a live desktop session.
