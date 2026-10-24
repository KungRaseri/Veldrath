# Veldrath web client (SvelteKit)

The single web frontend for Veldrath: public site, auth, game client, Foundry,
editorial, admin, and moderation. The .NET server remains the API and SignalR hub;
SvelteKit mints short-lived game JWTs and Better Auth owns web sessions.

## Development

```sh
npm install
npm run dev
```

The dev server expects `Veldrath.Server` on `http://localhost:9000` and the auth
database configured in `.env` (see `.env.example` at the repository root).

## Building the web app

```sh
npm run build     # adapter-node output in build/
npm run preview
```

## Building the desktop bundle

The Avalonia desktop client embeds a static Svelte SPA in its WebView2 shell. Build it
before building/publishing `Veldrath.Client`:

```sh
npm run build:desktop   # adapter-static SPA output in build-desktop/
```

`Veldrath.Client.csproj` copies `build-desktop/` into the output as `SvelteApp/` when it
exists. During development the client's `DesktopUiServer` can also read
`web/client/build-desktop` directly. Set `VELDRATH_DESKTOP_UI` to override the location.

The desktop host serves the SPA from a loopback port and proxies `/api` and `/hubs` to the
game server, injecting the desktop client's JWT server-side. The WebView never holds a
token. The native Avalonia UI remains the fallback when WebView2 is unavailable.

## Account and character flows

- `/register`, `/login` (email **or** username), `/forgot-password`, `/reset-password`, and
  `/verify-email` cover the full credential lifecycle.
- `/account` edits the display name and username, changes the email (confirmation required),
  resends verification, links/unlinks Discord/Google/Microsoft, lists active sessions with
  revoke actions, changes the password, and signs out.
- `/characters` shows roster capacity, a resume-last-session card, character deletion, and
  the entry point to `/characters/new`.
- `/characters/new` is a seven-step wizard (name, class, species, background, point-buy
  attributes, equipment, review/difficulty) backed by the .NET character-creation session API.
- Transactional email is sent through the .NET server's SMTP configuration via the internal
  bridge (`src/lib/server/email.ts`). Without `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN`
  the reset/verification links are logged to the SvelteKit server console for local testing.

## Checks

```sh
npm run typecheck
npm run lint
npm run test        # Vitest + Playwright
```
