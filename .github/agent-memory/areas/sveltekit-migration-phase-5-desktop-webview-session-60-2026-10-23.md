---
kind: area
date: 2026-10-23
area: engine
title: "SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23)"
---

## SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23)

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
