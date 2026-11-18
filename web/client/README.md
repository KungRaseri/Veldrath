# Veldrath web client (SvelteKit)

**The Veldrath game client, the Foundry content editor, and the whole web frontend** — public
site, auth, game, Foundry, editorial, admin, and moderation (web-first is the product decision;
there is no desktop client). The .NET server remains the API and SignalR hub; SvelteKit mints
short-lived game JWTs and Better Auth owns credentials and sessions.

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
  `/verify-email` cover the full credential lifecycle. Better Auth is the only authentication
  path — there is no ASP.NET Core Identity login to keep in sync.
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
- Typography: Cinzel / Cinzel Decorative / Lora / JetBrains Mono self-hosted in `static/fonts/`;
  Inter loads from Google Fonts with a `system-ui` fallback.
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

### Workflow e2e (needs the isolated stack)

The journey specs (game loop, encounters, Foundry, staff portals, editorial, public pages) talk to a
real `Veldrath.Server` and a real Postgres, because every page is rendered by a `+page.server.ts` load
that calls the API — a browser mock would never see the request. They cannot share a port or a database
with a development environment, so the stack is its own compose project on its own ports:

| Piece    | Port    | Notes                                                                                         |
| -------- | ------- | --------------------------------------------------------------------------------------------- |
| Postgres | `55432` | project `veldrath-e2e`, own volume (`docker-compose.e2e.yml`)                                 |
| API      | `9100`  | started by the script only; it never kills a server it did not start                          |
| Preview  | `4273`  | built and served by Playwright, with `ORIGIN`/`BETTER_AUTH_URL` and both public URLs injected |

```sh
./scripts/e2e-stack.sh start --fresh   # empty volume, so a green run means "works against the seed"
cd web && npm run test:e2e:stack --workspace client
./scripts/e2e-stack.sh stop
```

`npm run test:e2e:stack` runs the parallel specs, then `upstream-failure.e2e.ts` alone on one worker —
that spec stops the API to prove the pages say so, so it must not share a run with anything else. The
script also sets `Email__SmtpHost=""` (no SMTP container here) and `AUTH_RATE_LIMIT_DISABLED=true` (the
specs register dozens of accounts a minute and would otherwise meet a shared 429 bucket).

Individual specs: `E2E_STACK=1 E2E_WEB_PORT=4273 E2E_API_PORT=9100 E2E_DB_PORT=55432 npx playwright test tests/<spec>`.
CI runs the same split in the authenticated jobs of [`ci-web.yml`](../../.github/workflows/ci-web.yml).
