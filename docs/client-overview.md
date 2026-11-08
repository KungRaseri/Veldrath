# Client overview

The SvelteKit app under [`web/`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/) is the Veldrath client — public site, accounts,
characters, the game itself, the Foundry content editor, editorial, admin, and moderation.
`Veldrath.Server` stays the game API and SignalR authority, and Better Auth owns credentials and
sessions.

## Workspaces

`web/` is an npm workspace with two packages:

| Package | Role |
| --- | --- |
| `web/client` — `@veldrath/web` | The SvelteKit app: every route, the game shell, and the SvelteKit server endpoints |
| `web/shared` — `@veldrath/shared` | Better Auth's Drizzle schema, the database client, and database tooling |

The client is SvelteKit 3 on Svelte 5 with `adapter-node`, styled with Tailwind 4, Skeleton 5 and
bits-ui, and talks to the server over REST and `@microsoft/signalr`.

## Routes

Routes are grouped by audience. `(foundry)`, `(editorial)`, `(admin)` and `(mod)` are SvelteKit
route groups: they share layouts without contributing a URL segment.

| Area | Routes | Audience |
| --- | --- | --- |
| Public | `/`, `/community`, `/lore`, `/lore/[slug]`, `/patch-notes`, `/leaderboard`, `/leaderboard/hardcore`, `/players/[id]` | everyone |
| Accounts | `/register`, `/login`, `/forgot-password`, `/reset-password`, `/verify-email`, `/account` | everyone |
| Characters | `/characters`, `/characters/new` | signed in |
| Game | `/play/[id]`, `/game` | signed in |
| Foundry | `(foundry)/content`, `(foundry)/content/[type]/[slug]`, `(foundry)/submissions`, `(foundry)/languages`, `(foundry)/notifications` | contributors |
| Editorial | `(editorial)/editorial`, `…/announcements`, `…/lore`, `…/patch-notes` | editorial |
| Admin | `(admin)/admin` — users, roles, sessions, server, reports, audit, status | admins |
| Moderation | `(mod)/mod` — reports and users | moderators |

`/play/[id]` and `/game` render the full-screen game shell; the shared site header and footer are
hidden there.

## Authentication and the game token

1. Better Auth owns sign-up, sign-in, email verification, password reset, OAuth links and sessions.
   Its handler is mounted at `/api/auth/[...all]`.
2. `/api/game-token` mints a short-lived game JWT for the signed-in user.
3. `Veldrath.Server` validates that JWT, provisions the account just in time, and adds RBAC claims
   from Identity roles and permissions.
4. Password-reset and verification mail is relayed to the .NET server's SMTP transport through the
   internal bridge (`src/lib/server/email.ts`).

Transactional endpoints that exist only to talk to the .NET server live under `src/routes/api/` —
for example `/api/characters/creation/**`, which drives the server's character-creation session
across the seven-step wizard.

## Realtime

The game uses SignalR. The client invokes hub methods through `hub.invoke(...)` and renders the
resulting events in the game shell (`src/lib/game/`), which holds the reducers, point-buy maths,
cardinal-layout rules, and the panel components (sidebars, combat panel, tooltips, quest tracker).

## Contract parity

`src/lib/parity/` contains tests that pin the two sides together:

- **REST routes** — every path the web client calls must exist on the server, and every one of the
  server's ~140 minimal-API routes must be consumed or listed with a reason.
- **Hub methods and events** — every `hub.invoke` must match a method on `GameHub`, and every hub
  method must have a documented caller.

Because nothing generates one side from the other, these tests turn a renamed endpoint or hub
method into a build failure instead of a runtime `Failed to invoke`.

## Design and styling

The client is themed by two files, both of which this documentation site reuses:

- [`theme.css`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/routes/theme.css) — the Veldrath design system tokens
  (`--vds-*`): warm charcoal surfaces, Crimson Seal, Emberfall, Gold, the type scale and radii.
  docfx copies this file verbatim, so the docs site and the game cannot drift apart.
- [`brand.css`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/routes/brand.css) — self-hosted `@font-face` rules, base
  typography, site chrome, auth cards and game-surface primitives.

Typography is Cinzel and Cinzel Decorative for display and headings, Lora for long-form reading,
JetBrains Mono for code, and Inter for UI text. Fonts are self-hosted from `static/fonts/`.

The full specification — palette, type scale, spacing, component contracts, and the decision log —
is [the design system](design-system.md). UI-level decisions (panel-driven UI over 2D map
rendering, and the DB-backed map graph) are recorded in
[client UI architecture](client-ui-architecture.md).

## Where to start reading

| Path | What lives there |
| --- | --- |
| `src/routes/` | Every page and the SvelteKit `+server.ts` endpoints |
| `src/lib/server/` | All calls to `Veldrath.Server`, access control, admin API, email bridge |
| `src/lib/game/` | Game shell: session, reducers, point-buy, cardinal layout, panel components |
| `src/lib/foundry/` | The content editor |
| `src/lib/components/` | Shared site header/footer, auth components, pager |
| `src/lib/parity/` | REST/hub contract tests |
| `src/lib/testing/` | Test harness (fake app state, events, fetch) |

## Checks

```sh
npm run typecheck          # svelte-check
npm run lint               # prettier --check + eslint
npm run test               # vitest, then Playwright e2e
npm run coverage           # vitest coverage
```

Run them from `web/` (or `web/client/` for the client package alone). The repository-level
commands are documented in [`web/README.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/README.md) and
[`web/client/README.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/README.md).
