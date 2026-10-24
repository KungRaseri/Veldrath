# llms/ — Third-Party LLM Reference Files

This folder holds official `llms.txt` files (the [llmstxt.org](https://llmstxt.org) standard) downloaded from **other projects' documentation**. They are the **reference corpus** that the root agent skills in [`.roo/skills`](../../.roo/skills) are built from.

We do **not** create an `llms.txt` for this project itself — this folder is only for external reference material.

## Structure

One subdirectory per project, keeping the upstream file names so provenance stays clear:

```
llms/
  betterauth/
    llms.txt             # Better Auth docs (better-auth.com/llms.txt)
  drizzle/
    llms.txt             # Drizzle ORM + drizzle-kit docs (orm.drizzle.team/llms.txt)
  sveltekit/
    llms-full.txt        # combined Svelte 5 + SvelteKit docs (svelte.dev + kit.svelte.dev)
  zod/
    llms.txt             # Zod v4 reference (zod.dev/llms.txt)
  ...
  README.md
```

## Currently present

| Folder | Source | Used for |
| --- | --- | --- |
| `sveltekit/` | svelte.dev + kit.svelte.dev | Svelte 5 runes + SvelteKit conventions → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `betterauth/` | better-auth.com | Better Auth → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `drizzle/` | orm.drizzle.team | Drizzle ORM + drizzle-kit → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `vite/` | vite.dev | Vite dev/build/HMR → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `vitest/` | vitest.dev | Vitest (unit/browser/coverage/mocking) → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `zod/` | zod.dev | Zod v4 validation reference (transitive dependency only) |
| `tailwindcss/` | hand-built, verified against live docs nav 2026-08-04 (v4.3) | Tailwind v4 (CSS-first `@theme`, `@utility`, variants, forms/typography plugins) → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `bitsui/` | bits-ui.com (`llms.txt` index + `llms-full.txt` from `bits-ui.com/docs/llms.txt`, fetched 2026-08-06) | Bits UI headless Svelte components (tabs, popover, tooltip, dropdown, dialog, combobox, meter, slider, avatar, scroll area). **`llms-full.txt` is the offline reference** (the `llms.txt` index is kept untouched) → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `skeletondev/` | skeleton.dev (`llms.txt` index + `llms-full.txt`) | Skeleton v5 design system (themes, tokens, color pairings, presets, Svelte components, Tailwind utilities) → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `lucide/` | lucide.dev (`llms.txt` index only — **no full file published**; verified 2026-08-06) | Lucide icon set for general UI icons. Authoritative icon inventory is the installed package `node_modules/@lucide/svelte/dist/icons/*`, imported per-icon via `@lucide/svelte/icons/<kebab-name>` → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |

## Where files come from

Many projects publish `llms.txt` at their docs root (and often `llms-full.txt` with the full content inlined). Known publishers:

- Svelte: `https://svelte.dev/llms.txt`
- SvelteKit: `https://kit.svelte.dev/llms.txt`
- Better Auth: `https://www.better-auth.com/llms.txt`
- Drizzle: `https://orm.drizzle.team/llms.txt`
- zod: `https://zod.dev/llms.txt`
- Bits UI: `https://bits-ui.com/docs/llms.txt` (the complete Bits UI docs, incl. all components, type helpers, utilities)
- Lucide: `https://lucide.dev/llms.txt` (index only — no full file; the authoritative icon inventory is the installed `@lucide/svelte` package, `node_modules/@lucide/svelte/dist/icons/*`)
- Others: check the project's docs site for `/llms.txt` (some projects, e.g. Tailwind, intentionally do not publish one — handle those later with a hand-built reference).

## Recommended additions

Prioritized by how much this monorepo uses the project. Check each docs site for `/llms.txt`.

| Priority | Project | Why it matters here | Docs root to check |
| --- | --- | --- | --- |
| High | TypeScript | strict TS + ESM everywhere; tsconfig and type-level patterns | typescriptlang.org |
| Medium | Playwright | client e2e (`client/playwright.config.ts`) | playwright.dev |
| Medium | Node.js | engines `node >= 20`; `node:process`/`node:url`/`node:path` used in configs and server | nodejs.org |
| Medium | ESLint | flat config + typescript-eslint across workspaces | eslint.org |
| Medium | postgres.js (porsager/postgres) | client DB driver | porsager/postgres |
| Medium | pg / node-postgres | server DB driver | node-postgres.com |
| Low | ws | WebSocket server | github.com/websockets/ws |
| Low | npm workspaces | monorepo orchestration, scripts, lockfile | docs.npmjs.com |
| Low | tsx | server runner (`tsx watch src/index.ts`) | tsx.is |
| Low | Prettier | formatting across the repo | prettier.io |
| Low | mdsvex | client markdown preprocessor | mdsvex.com |

Handled: **Tailwind CSS** — its authors deliberately don't publish an `llms.txt`, so it is hand-built at `llms/tailwindcss/llms.txt` (v4.3, grounded in `client/src/routes/layout.css`).

## Usage

1. Create a subdirectory per project and drop the downloaded file(s) in.
2. Skills in [`../../.roo/skills`](../../.roo/skills) reference these files by path in their **References** section.
3. After adding a file, open the relevant skill(s) and tune the steps to match the actual content of the file.
