---
name: web-development
description: Use when building or changing the SvelteKit web app in web/client — routes, runes, load functions, form actions, Better Auth sessions, Drizzle migrations, environment access, or the build/adapter configuration.
---

# Skill: web-development

Build and change the SvelteKit web app ([`web/client`](../../../web/client)) that serves the public site, auth, game client, Foundry, editorial, admin, and moderation routes.

## When to use

- Adding or changing routes (`+page.svelte`, `+page.server.ts`, `+layout.svelte`, `+layout.server.ts`, `+server.ts`).
- Writing Svelte 5 runes-based components, snippets, and state.
- Implementing form actions, SSR data loading, or environment access.
- Changing Better Auth wiring, sessions, OAuth providers, or the Drizzle schema/migrations.
- Changing the build/deploy config (`vite.config.ts`, adapter-node vs adapter-static desktop build).

## When not to use

- Visual design, tokens, or component styling — use [`web-ui`](../web-ui/SKILL.md).
- Engine/game-logic changes — use [`create-new-feature`](../create-new-feature/SKILL.md).
- Server hub or endpoint changes — use [`run-server`](../run-server/SKILL.md) plus [`.github/instructions/hub-development.md`](../../../.github/instructions/hub-development.md).

## References

- [`AGENTS.md`](../../../AGENTS.md) → **Web App (SvelteKit)**, “Hard Rules”, “Build & Test Commands”.
- [`web/README.md`](../../../web/README.md) and [`web/client/README.md`](../../../web/client/README.md) — workspace layout, commands, account/character flows.
- [`web/client/src/lib/server/`](../../../web/client/src/lib/server) — server-only code (`auth.ts`, `db/`, `game-api.ts`, `game-request.ts`, `email.ts`, `foundry-bridge.ts`).
- [`web/client/src/hooks.server.ts`](../../../web/client/src/hooks.server.ts) — `handle` → `svelteKitHandler`, populates `event.locals.user` / `event.locals.session`, and runs the one-time account bridge.
- [`web/client/src/app.d.ts`](../../../web/client/src/app.d.ts) — `App.Locals` / `AuthUser` types.
- [`web/shared/src/db/schema.ts`](../../../web/shared/src/db/schema.ts) and [`web/shared/src/db/auth.schema.ts`](../../../web/shared/src/db/auth.schema.ts) — Drizzle schema (Better Auth tables included).
- [`web/client/vite.config.ts`](../../../web/client/vite.config.ts) — SvelteKit plugin (runes forced), adapter selection, Vitest projects.
- [`web/llms/sveltekit/llms-full.txt`](../../../web/llms/sveltekit/llms-full.txt), [`web/llms/betterauth/llms.txt`](../../../web/llms/betterauth/llms.txt), [`web/llms/drizzle/llms.txt`](../../../web/llms/drizzle/llms.txt), [`web/llms/vitest/llms-full.txt`](../../../web/llms/vitest/llms-full.txt).

## Key facts

- **Workspaces:** `npm` workspaces live in [`web/package.json`](../../../web/package.json) (`shared`, `client`). Run all root scripts from `web/`, not `web/client/`.
- **Svelte 5 runes only.** Runes mode is forced in `vite.config.ts`; use `$state`, `$derived`, `$props`, `$bindable`. No `export let`, no `$:`.
- **Route layout is flat:** `web/client/src/routes/<route>/` with per-route `+page.server.ts` guards. Do not reintroduce route groups from the original template.
- **Server-only boundary:** `$lib/server`, `$env/*/private`, and `hooks.server.*` must never be imported (directly or indirectly) by client components — SvelteKit fails the build.
- **Auth:** Better Auth owns web credentials/sessions. Keep `getAuth()` and the Postgres client lazy so `vite build` never touches the database.
- **Game tokens:** SvelteKit mints short-lived JWTs for `Veldrath.Server`; all game API calls go through `gameApiFetch` in `src/lib/server/game-request.ts`.
- **Email:** transactional mail is sent through the .NET server's SMTP service via the internal bridge (`src/lib/server/email.ts`); without `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN` links are logged locally.
- **Adapters:** `adapter-node` for the deployed web app; `adapter-static` SPA output for the Avalonia WebView2 shell (`npm run build:desktop`).

## Steps

1. Read the matching `AGENTS.md` section and confirm which workspace the change belongs to.
2. Follow existing patterns in `web/client/src/routes/` and `web/client/src/lib/`; prefer editing an existing route over adding a parallel one.
3. Keep DB/auth imports server-side and lazy; put shared logic in `src/lib/server/` or `web/shared/`.
4. Use typed imports from `src/lib/game/types.ts` for hub payloads rather than inline shapes.
5. For schema changes: edit `web/shared/src/db/schema.ts` → `npm run db:generate` → `npm run db:migrate` (from `web/`); commit the generated migration.
6. Verify once at the end from `web/`:
   ```sh
   npm run typecheck
   npm run lint
   npm run test
   npm run build
   ```
   Add `npm run build:desktop` when the change affects what the desktop WebView shell renders.

## Rules

- Never import server-only modules into components or `+page.svelte` files.
- Never construct auth or database clients at module scope.
- Do not store secrets or JWTs in `localStorage` or client state; tokens stay server-side.
- Every public route that requires an account must validate the session in its `+page.server.ts` load and `throw redirect(...)` / `error(...)`.
- Keep the UI a read-only view of authoritative state: components send typed commands, never mutate game state directly.
- Do not suppress Svelte/TypeScript diagnostics to make a check pass; fix the underlying typing.
