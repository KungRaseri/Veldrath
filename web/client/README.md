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

## Theme and assets

- [`src/routes/theme.css`](src/routes/theme.css) maps Skeleton v5 tokens to the Veldrath design
  system (Crimson Seal, Emberfall, Gold, warm charcoal surfaces).
- [`src/routes/brand.css`](src/routes/brand.css) owns self-hosted `@font-face` rules, base
  typography, site chrome, auth cards, and game-surface primitives.
- Typography: Cinzel / Cinzel Decorative / Lora / JetBrains Mono in `static/fonts/` (from
  `RealmUI.Fonts`); Inter loads from Google Fonts with a `system-ui` fallback.
- Brand imagery and favicons live in `static/images/`; keep `static/images/favicon/site.webmanifest`
  in sync with the files there.
- UI icons use `@lucide/svelte`. Brand glyph paths for Discord/Google/GitHub are vendored in
  `src/lib/brand-icons.ts` to avoid bundling all of Simple Icons.
- [`src/lib/components/site/`](src/lib/components/site/) holds the shared site header/footer;
  both are hidden on `/play/*` and `/desktop`, which render the full-screen game shell.

## Checks

```sh
npm run typecheck
npm run lint
npm run test        # Vitest + Playwright
```

### Game-loop e2e (needs a running server)

`tests/game-loop.e2e.ts` covers the authenticated path — register → roster → play → travel →
inventory — and skips itself unless `E2E_GAME=1`. It cannot be mocked: the roster is rendered by
SSR through `/api/characters` and the game shell needs a live SignalR hub.

```sh
# 1. start the API with the preview origin allowed
cd Veldrath.Server && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:9000 \
  Cors__AllowedOrigins__0=http://localhost:4173 dotnet run --no-launch-profile

# 2. run the spec
cd web/client && E2E_GAME=1 E2E_API_URL=http://127.0.0.1:9000 \
  ORIGIN=http://localhost:4173 BETTER_AUTH_URL=http://localhost:4173 \
  PUBLIC_API_URL=http://127.0.0.1:9000 PUBLIC_HUB_URL=http://127.0.0.1:9000/hubs/game \
  INTERNAL_BRIDGE_URL=http://127.0.0.1:9000 npm run test:e2e:game
```

CI runs the same thing in the `e2e-game` job of [`ci-web.yml`](../../.github/workflows/ci-web.yml).
