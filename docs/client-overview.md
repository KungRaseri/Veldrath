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
   internal bridge.

The client exposes a few endpoints of its own that exist only to talk to the .NET server —
for example `/api/characters/creation/**`, which drives the server's character-creation session
across the seven-step wizard.

## Realtime

The game uses SignalR. The client invokes hub methods through `hub.invoke(...)` and renders the
resulting events in the game shell, which holds the reducers, point-buy maths,
cardinal-layout rules, and the panel components (sidebars, combat panel, tooltips, quest tracker).
