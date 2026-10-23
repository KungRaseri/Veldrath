# AGENTS.md — Monorepo Template

Reference document for AI agents (and humans) working in this repository. This is a reference, not a tutorial.

## Project overview

This is a **TypeScript npm-workspaces monorepo template** built around a pure logic/domain
workspace, a **Colyseus** server hosting authoritative WebSocket sessions, and a SvelteKit client.

- The TypeScript codebase lives in the four workspaces: [`shared/`](shared), [`engine/`](engine),
  [`server/`](server), [`client/`](client).
- Architecture and design documentation lives in [`docs/`](docs). Add your own docs here and keep
  them in sync as the architecture changes.
- Reusable **agent skills** live in [`.roo/skills/`](.roo/skills) and their third-party `llms.txt`
  reference corpus in [`llms/`](llms) — see [Skills & reference material](#skills--reference-material).

> **Adopting this template:** replace every `your-project` / `@your-scope` placeholder with your own
> name/scope, then delete or rewrite the placeholder sections below. A step-by-step guide is in
> [`TEMPLATE-GUIDE.md`](TEMPLATE-GUIDE.md).

## Repository layout

| Path | Package | Role |
| --- | --- | --- |
| [`shared/`](shared) | `@your-scope/shared` | Shared contracts (wire protocol types, DTOs, enums, constants) **and the single Drizzle schema + DB config**. Subpath exports: `@your-scope/shared/db` → [`shared/src/db/index.ts`](shared/src/db/index.ts), `@your-scope/shared/schema` (Colyseus room schemas). |
| [`engine/`](engine) | `@your-scope/engine` | Pure TypeScript logic engine (domain, systems, persistence). Deterministic, **no I/O, no network**. Depends only on `@your-scope/shared`. |
| [`server/`](server) | `@your-scope/server` | **Colyseus** server hosting authoritative sessions in **rooms**; Postgres persistence via `pg`/Drizzle. |
| [`client/`](client) | `client` (unscoped) | SvelteKit app (Svelte 5 runes, Vite, Tailwind 4, adapter-node, mdsvex) using the `@colyseus/sdk` SDK. |
| [`docs/`](docs) | — | Architecture + design documentation (write your own; keep in sync). |
| [`.roo/skills/`](.roo/skills) | — | Reusable agent skills (one folder per skill, each a `SKILL.md`: name/description frontmatter + when-to-use, references, steps). Catalog in [`.roo/skills/README.md`](.roo/skills/README.md). |
| [`llms/`](llms) | — | Third-party `llms.txt` reference files (official docs from SvelteKit, Drizzle, Better Auth, Tailwind, Vite, Vitest, Zod) that skills reference by path. Provenance in [`llms/README.md`](llms/README.md). |

## Skills & reference material

The repo ships reusable, tool-agnostic **agent skills** that turn this document, the docs, and
third-party references into repeatable workflows:

- **Catalog:** [`.roo/skills/README.md`](.roo/skills/README.md) lists every skill and when to use it.
  Start any new task with [`.roo/skills/repo-context/SKILL.md`](.roo/skills/repo-context/SKILL.md)
  for orientation, then pick the matching skill for the work (`database-workflow`, `auth-setup`,
  `sveltekit-development`, `ui-development`, `verification`, `new-feature`).
- **Format:** each skill is a folder with a `SKILL.md` (`name`/`description` frontmatter + when-to-use,
  references, steps). New skills copy [`.roo/skills/_template/SKILL.md`](.roo/skills/_template/SKILL.md).
- **Reference corpus:** [`llms/`](llms) holds official `llms.txt` files from external projects.
  Skills reference them by path; [`llms/README.md`](llms/README.md) records provenance and source URLs.
- **Keep in sync:** update the affected skills when the architecture changes; when a new `llms/` file
  is dropped in, tune the skill(s) that reference it. `llms/` is external reference only — never treat
  it as project documentation.

## Tech stack & tooling

- **TypeScript**, strict, **ESM** everywhere (`"type": "module"`; root `engines.node >= 20`).
- **npm workspaces** (root workspace `your-project`) + `concurrently`/`wait-on` for orchestration.
- **SvelteKit** (client), Svelte 5 runes, Tailwind CSS 4, `@sveltejs/adapter-node`.
- **UI stack (client):** **Skeleton v5** (`@skeletonlabs/skeleton` + `@skeletonlabs/skeleton-svelte`)
  as the design system (custom theme `app`), **Bits UI** (`bits-ui`) headless primitives for
  complex components, **Lucide** (`@lucide/svelte`) for general icons, **Simple Icons**
  (`simple-icons`) for brand marks.
- **Colyseus** (`@colyseus/core` + `@colyseus/ws-transport` + `@colyseus/schema` on the server,
  `@colyseus/sdk` on the client) — the **transport + state-sync foundation**. The `colyseus` umbrella
  package is **not** a dependency; the server imports `@colyseus/core`/`@colyseus/ws-transport`
  directly. Room schemas live in `@your-scope/shared/schema`.
- **Vitest** for unit tests (engine, shared, server; client has node + browser projects) and
  **Playwright** for e2e (`client`).
- **ESLint** (flat config, `typescript-eslint`) + **Prettier**.
- **Drizzle ORM** + **drizzle-kit** for the database; drivers wired per workspace: `postgres`
  (postgres.js) in the client, `pg` in the server.
- **Better Auth** for authentication (client).
- **tsx** for running the TypeScript server (`dev`/`start`).

## Commands (from repo root)

Install once at the root — installs all workspaces:

```bash
npm install
```

Run everything (`--workspaces --if-present`):

```bash
npm run build       # tsc --noEmit per workspace (client: vite build)
npm run test        # vitest run per workspace (+ Playwright in client)
npm run coverage    # vitest run --coverage per workspace
npm run typecheck   # tsc --noEmit per workspace
npm run lint        # eslint per workspace (client also runs prettier --check)
npm run format      # prettier --write .
npm run format:check
```

Dev servers:

```bash
npm run dev         # concurrently: server then (wait-on tcp:3000) client
#   server → tsx watch src/index.ts   → ws://localhost:3000
#   client → vite dev                 → http://localhost:5173
```

Database (forwarded to the `shared` workspace):

```bash
npm run db:generate   # drizzle-kit generate  → shared/db/migrations
npm run db:migrate    # drizzle-kit migrate
npm run db:push       # drizzle-kit push (dev-time schema sync)
npm run db:studio     # drizzle-kit studio
```

Per-workspace scripts: `npm run <script> --workspace <name>`. Notable ones:

- `server`: `dev` (`tsx watch src/index.ts`), `start` (`tsx src/index.ts`).
- `client`: `dev`, `build`, `check` (`svelte-check`), `test:unit`, `test:e2e`, `auth:schema`.
- `shared`/`engine`/`server`: `build`, `typecheck`, `test`, `coverage`, `lint`.
- `shared`: `db:generate`, `db:migrate`, `db:push`, `db:studio`.

## Environment & setup

Each process owns its `.env` file, both loaded via dotenv (git-ignored; templates
[`client/.env.example`](client/.env.example) and [`server/.env.example`](server/.env.example)):

- [`client/.env`](client/.env) — SvelteKit / Better Auth: `DATABASE_URL`, `ORIGIN`,
  `BETTER_AUTH_SECRET`, `WS_URL`, `WS_AUTH_SECRET`.
- [`server/.env`](server/.env) — game server: `PORT` (default 3000), `HOST`, `DATABASE_URL`,
  `WS_AUTH_SECRET`.

`DATABASE_URL` and `WS_AUTH_SECRET` are shared between the two processes and must match in both files.

Setup:

```bash
copy client\.env.example client\.env   # Windows (cp client/.env.example client/.env elsewhere)
copy server\.env.example server\.env   # Windows (cp server/.env.example server/.env elsewhere)
```

Who loads it:

- Client: SvelteKit reads `client/.env` via its dotenv integration — access through
  `$env/dynamic/private` (server-only modules).
- Server: [`server/src/index.ts`](server/src/index.ts) `main()` loads `server/.env` via dotenv
  (existing env vars win).
- Drizzle: [`shared/drizzle.config.ts`](shared/drizzle.config.ts) loads `server/.env` (fallback
  `client/.env`) via Node `loadEnvFile`, so root `npm run db:*` works without exporting `DATABASE_URL`.

Git-ignore behavior: `.env` / `.env.*` are ignored at the root (patterns match every workspace);
`.env.example` files are explicitly un-ignored (`client/.gitignore` additionally un-ignores `.env.test`).

## Database workflow

- **Schema source of truth:** [`shared/src/db/schema.ts`](shared/src/db/schema.ts) — driver-agnostic
  `drizzle-orm/pg-core` definitions. It re-exports the Better Auth tables from
  [`shared/src/db/auth.schema.ts`](shared/src/db/auth.schema.ts) (`user`, `session`, `account`,
  `verification`). Add your own tables here.
- **Import point:** `@your-scope/shared/db` (→ [`shared/src/db/index.ts`](shared/src/db/index.ts))
  re-exports the schema. Workspaces import the schema from this subpath so the shared protocol types
  stay free of Drizzle dependencies.
- **Per-workspace drivers, same schema:** client uses `postgres` via the lazy `getAuthDb()`
  singleton; server uses `pg` via its persistence layer. Table definitions never drift between
  workspaces.
- **Migrations:** generated into `shared/db/migrations/` (Better Auth tables + your tables).
- **Workflow:** edit `shared/src/db/schema.ts` (or `auth.schema.ts`) → `npm run db:generate` →
  `npm run db:migrate` (or `db:push` for dev). All run from the root.
- **Auth schema regeneration:** `npm run auth:schema --workspace client` (runs
  `auth generate --config src/lib/server/auth.ts --output ../shared/src/db/auth.schema.ts --yes`) to
  regenerate the Better Auth tables after upgrading Better Auth.
- **Keep `getAuthDb()` lazy:** the client DB singleton deliberately defers constructing the `postgres`
  client until first call, so `vite build` (including SvelteKit's postbuild `analyse` step) stays
  DB-free. Do not construct DB or auth clients at module scope.

## Auth architecture

- Better Auth instance: [`client/src/lib/server/auth.ts`](client/src/lib/server/auth.ts) — lazy
  `getAuth()` singleton over the shared-schema Drizzle client, drizzle adapter with `provider: 'pg'`,
  email/password enabled, `sveltekitCookies` plugin (must stay the last plugin in the array).
- Wired in [`client/src/hooks.server.ts`](client/src/hooks.server.ts) via `svelteKitHandler`; sets
  `event.locals.user` / `event.locals.session`.
- Locals types in [`client/src/app.d.ts`](client/src/app.d.ts)
  (`App.Locals.user?: User`, `session?: Session`).
- Auth routes: create `(public)/login` + `(public)/register` following Better Auth's SvelteKit
  integration; put authed UI behind a `(app)` route group.

## Real-time server architecture

- **Colyseus transport only.** `GameRoom` per player (`onAuth` verifies the HMAC ticket via the
  shared ticket module; returns the payload as `client.auth`) + any shared broadcast rooms.
  Colyseus is **transport + state-sync only** — it never touches the engine, and its DB is our
  Drizzle (no Colyseus DB driver). The `@colyseus/auth` module is **NOT used** — Better Auth remains
  the auth system.
- **Room schemas** live in `@your-scope/shared/schema` (Colyseus Schema classes, state-only, no game
  logic — per Colyseus best practices).
- **Server owns authoritative sessions**; the client is a read-only view that sends typed commands.

## Web UI & design system

- **Skeleton v5 is the design system** for the whole app, on Tailwind 4, custom theme `app`
  (wired in [`client/src/routes/theme.css`](client/src/routes/theme.css) +
  [`client/src/app.html`](client/src/app.html)).
- **Bits UI** provides headless behavior/a11y for complex components (tabs, popover, tooltip,
  dropdown, dialog, combobox, meter, slider, avatar, scroll area).
- **Lucide** for general icons; **Simple Icons** for brand marks only. **No emoji as UI icons.**
- **Zero hardcoded colors** — use Skeleton tokens (`primary-*`, `surface-*`, `preset-*`, …).
- **No custom CSS / inline styles** unless absolutely necessary — Tailwind utilities + Skeleton
  presets only.
- Component org: `$lib/components/site/*` (site chrome), `$lib/components/ui/*` (shared atoms),
  `$lib/components/*` (feature components).
- **Read-only UI contract:** components only read store/state and send typed commands; never mutate
  authoritative state.

## Conventions

- TypeScript strict + ESM throughout; no `CommonJS`.
- Drizzle schema stays driver-agnostic — never import a driver-specific module (e.g. `pg`/`postgres`)
  into `shared/src/db/*`.
- Run `npm run typecheck` and `npm run lint` (plus relevant tests) ONCE at the END of the change
  session — batch all your edits first, then verify. Do not run them after every individual change.
- Keep `docs/` in sync when architecture changes.
- Each workspace owns its env file (`client/.env`, `server/.env`); keep shared values
  (`DATABASE_URL`, `WS_AUTH_SECRET`) in sync across them.
- Keep DB/auth construction lazy (`getAuthDb()`/`getAuth()`) so builds stay DB-free.
- **Keep Colyseus out of `engine/` and out of the shared protocol layer.** `engine/` stays pure
  (no `colyseus`, no network). `@your-scope/shared` main entry stays framework-free; Colyseus
  schemas live only under the `@your-scope/shared/schema` subpath.
- **Never adopt `@colyseus/auth`** — Better Auth is the auth system; the server verifies stateless
  HMAC tickets in `onAuth` and never reads auth tables.
- **Web UI rules:** Skeleton tokens/presets for all styling, Lucide icons, no emoji, no custom
  CSS/inline styles, Svelte 5 runes, read-only UI contract. Verify client changes ONCE at the END of
  a UI change session — batch all your UI edits first, then run `npm run check --workspace client` +
  `npm run lint --workspace client` + `npm run test:unit --workspace client`. Do not run them after
  every change.
