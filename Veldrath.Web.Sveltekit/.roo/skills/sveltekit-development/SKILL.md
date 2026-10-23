---
name: sveltekit-development
description: Use when building or changing the SvelteKit client — routes, components, runes, forms, SSR, environment access, or the build/adapter config.
---

# SvelteKit Development

## When to use

- Creating routes (`+page.svelte`, `+page.server.ts`, `+layout.svelte`, `+server.ts`, load functions).
- Writing Svelte 5 components (runes mode).
- Implementing forms/actions, SSR, or reading environment variables.
- Changing the build/deploy config (adapter-node, vite).

## When NOT to use

- Visual design, styling, or Skeleton/Bits UI/Lucide component work — use `ui-development`.
- Anything outside the client workspace (engine, server rooms, DB schema) — use the matching skill.

## References

- [`llms/sveltekit/llms-full.txt`](../../../llms/sveltekit/llms-full.txt) — official Svelte 5 + SvelteKit docs (in-repo reference):
  - Svelte runes: `# What are runes?` → `$state`, `$derived`, `$effect`, `$props`, `$bindable`, `$inspect`, `$host`
  - Markup blocks: `{#if ...}`, `{#each ...}`, `{#key ...}`, `{#await ...}`, `{#snippet ...}`
  - SvelteKit: `# Routing`, `# Loading data`, `# Form actions`, `# Page options`, `# Environment variables`, `# Adapters`, `# Hooks`, `# Server-only modules`
- [`llms/vite/llms-full.txt`](../../../llms/vite/llms-full.txt) — Vite docs (the client runs on Vite): `Features` (HMR), `Configuring Vite`, `Building for Production`.
- [`llms/tailwindcss/llms.txt`](../../../llms/tailwindcss/llms.txt) — Tailwind v4 (hand-built reference): CSS-first `@theme`, `@utility`, `@variant`, forms/typography plugins.
- [`AGENTS.md`](../../../AGENTS.md) → Tech stack, Environment & setup, Auth architecture.
- [`client/src/routes/`](../../../client/src/routes/) — route groups: `(public)`, `(app)` (auth-gated), `(admin)`; auth pages under `(public)/login` + `(public)/register`.
- [`client/src/lib/server/`](../../../client/src/lib/server/) — server-only code (`auth.ts`, `db/index.ts`).
- [`client/vite.config.ts`](../../../client/vite.config.ts) — sveltekit plugin (runes forced), adapter-node, mdsvex, vitest settings.
- [`client/tsconfig.json`](../../../client/tsconfig.json) — bundler module resolution; `@your-scope/shared` resolved as a package.

## Key facts (grounded in `llms/sveltekit/llms-full.txt`)

- **Svelte 5 runes** are compiler keywords — no import needed:
  - `$state` → deeply reactive proxies; `$state.raw` (reassign-only), `$state.snapshot` (unproxy).
  - `$derived` / `$derived.by` → derived values; expressions must be free of side effects.
  - `$effect` → runs in the browser only (not SSR); may return a teardown fn; **don't set state inside effects** — prefer `$derived`.
  - `$props` → destructure with fallbacks; `$bindable` for two-way (bound) props.
  - `$inspect` → dev-only reactive logging; `$host` → custom-element host.
- **Reusable markup** uses `{#snippet name(params)}` + `{@render name(...)}`; use keyed `{#each items as item (item.id)}` for lists.
- **Runes mode is forced** in this repo via `compilerOptions.runes` in `client/vite.config.ts`.
- **Env**: `.env` / `.env.local` are loaded in dev and at build time. Import via `$env/dynamic/private` (server-only), `$env/static/private`, `$env/dynamic/public`, or `$env/static/public`. `client/.env` is this workspace's env file (the server owns its own `server/.env`; `DATABASE_URL` and `WS_AUTH_SECRET` must match in both).
- **Server-only modules**: `$env/*/private` and `$lib/server` may only be imported by server-only code (`hooks.server.*`, `+page.server.*`, `.server` files, `$lib/server`). SvelteKit errors at build if public-facing code imports them (even indirectly).
- **Adapter**: this repo uses `@sveltejs/adapter-node` (see `client/vite.config.ts`).
- **Vite**: `client/vite.config.ts` is a Vite config loaded by the `sveltekit()` plugin. Dev runs `vite dev` (HMR); production runs `vite build` through adapter-node. See `llms/vite/llms-full.txt`.
- **Tailwind v4 (CSS-first)**: no `tailwind.config.js`; the entry `client/src/routes/layout.css` is `@import 'tailwindcss'` + `@plugin '@tailwindcss/forms'` + `@plugin '@tailwindcss/typography'` + the Skeleton + theme imports. Configure tokens via `@theme` in CSS. See `llms/tailwindcss/llms.txt`.

## Steps

1. Read the relevant section of `llms/sveltekit/llms-full.txt` before writing code (runes or SvelteKit features).
2. Follow existing patterns in `client/src/routes/` and `client/src/lib/`.
3. Keep DB/auth imports server-side (`client/src/lib/server`); keep `getAuthDb()`/`getAuth()` lazy.
4. After changes: `npm run check --workspace client` (svelte-check) and `npm run build --workspace client`.
5. Run client tests: `npm run test:unit --workspace client`.

## Rules

- Never import `client/src/lib/server` or `$env/dynamic/private` from client-side components — SvelteKit will error on the import chain.
- Don't construct DB/auth clients at module scope.
- Use runes idioms: prefer `$derived` over `$effect` for derived values; don't mutate props (use `$bindable`).
