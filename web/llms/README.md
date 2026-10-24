# llms/ — Third-Party LLM Reference Files

This folder holds official `llms.txt` files (the [llmstxt.org](https://llmstxt.org) standard) downloaded from **other projects' documentation**. They are the **reference corpus** that the root agent skills in [`.roo/skills`](../../.roo/skills) are built from.

We do **not** create an `llms.txt` for this project itself — this folder is only for external reference material.

**Snapshot:** refreshed **2026-10-24** to match the installed stack — SvelteKit 3.1 / Svelte 5, Vite 8.3, Vitest 5.0, Better Auth 1.7, Drizzle 0.45, Bits UI 2.20, Skeleton 5.0, Lucide 1.61, Zod 4.

## Structure

One subdirectory per project, keeping the upstream file names so provenance stays clear:

```
llms/
  betterauth/
    llms.txt             # Better Auth docs (better-auth.com/docs/llms.txt)
  bitsui/
    llms.txt             # Bits UI page index (bits-ui.com/llms.txt)
    llms-full.txt        # Complete Bits UI docs (bits-ui.com/docs/llms.txt)
  drizzle/
    llms.txt             # Drizzle ORM + drizzle-kit docs (orm.drizzle.team/llms.txt)
  lucide/
    llms.txt             # Lucide docs index (lucide.dev/llms.txt)
  skeletondev/
    llms.txt             # Skeleton docs index (skeleton.dev/llms.txt)
    llms-full.txt        # Complete Skeleton v5 docs (skeleton.dev/llms-full.txt)
  sveltekit/
    llms.txt             # Svelte/SvelteKit documentation-set index (svelte.dev/llms.txt)
    llms-full.txt        # Combined Svelte 5 + SvelteKit docs (svelte.dev/llms-full.txt)
  tailwindcss/
    llms.txt             # Hand-built Tailwind v4 reference (no upstream llms.txt)
  vite/
    llms.txt             # Vite docs index (vite.dev/llms.txt)
    llms-full.txt        # Complete Vite docs (vite.dev/llms-full.txt)
  vitest/
    llms.txt             # Vitest docs index (vitest.dev/llms.txt)
    llms-full.txt        # Complete Vitest docs (vitest.dev/llms-full.txt)
  zod/
    llms.txt             # Zod 4 docs (zod.dev/llms.txt)
  README.md
```

## Currently present

| Folder | Files | Source (retrieved 2026-10-24) | Used for |
| --- | --- | --- | --- |
| `sveltekit/` | `llms.txt`, `llms-full.txt` | [svelte.dev/llms.txt](https://svelte.dev/llms.txt), [svelte.dev/llms-full.txt](https://svelte.dev/llms-full.txt) | Svelte 5 runes + SvelteKit 3 (routing, load, actions, `$app/*`, adapters, `#lib`) → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `betterauth/` | `llms.txt` | [better-auth.com/docs/llms.txt](https://www.better-auth.com/docs/llms.txt) | Better Auth sessions, OAuth providers, plugins, SvelteKit integration → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `drizzle/` | `llms.txt` | [orm.drizzle.team/llms.txt](https://orm.drizzle.team/llms.txt) | Drizzle ORM + drizzle-kit schema, relations, migrations → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `vite/` | `llms.txt`, `llms-full.txt` | [vite.dev/llms.txt](https://vite.dev/llms.txt), [vite.dev/llms-full.txt](https://vite.dev/llms-full.txt) | Vite 8 dev/build/HMR and config → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `vitest/` | `llms.txt`, `llms-full.txt` | [vitest.dev/llms.txt](https://vitest.dev/llms.txt), [vitest.dev/llms-full.txt](https://vitest.dev/llms-full.txt) | Vitest 5 unit tests, coverage, mocking → [`.roo/skills/web-development`](../../.roo/skills/web-development/SKILL.md) |
| `zod/` | `llms.txt` | [zod.dev/llms.txt](https://zod.dev/llms.txt) | Zod 4 validation reference (transitive dependency only) |
| `tailwindcss/` | `llms.txt` | Hand-built, verified against live docs 2026-10-24 (v4.3) | Tailwind v4 CSS-first `@theme`, `@utility`, variants, forms/typography plugins → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `bitsui/` | `llms.txt`, `llms-full.txt` | [bits-ui.com/llms.txt](https://bits-ui.com/llms.txt), [bits-ui.com/docs/llms.txt](https://bits-ui.com/docs/llms.txt) | Bits UI headless Svelte components — `llms-full.txt` is the offline reference → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `skeletondev/` | `llms.txt`, `llms-full.txt` | [skeleton.dev/llms.txt](https://skeleton.dev/llms.txt), [skeleton.dev/llms-full.txt](https://skeleton.dev/llms-full.txt) | Skeleton v5 design system: themes, tokens, pairings, presets, Svelte components → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |
| `lucide/` | `llms.txt` | [lucide.dev/llms.txt](https://lucide.dev/llms.txt) | Lucide icon docs index. The authoritative icon inventory is the installed package: `node_modules/@lucide/svelte/dist/icons/*` (import per icon from `@lucide/svelte/icons/<kebab-name>`) → [`.roo/skills/web-ui`](../../.roo/skills/web-ui/SKILL.md) |

## Where files come from

| Project | Index | Full reference |
| --- | --- | --- |
| Svelte + SvelteKit | `https://svelte.dev/llms.txt` | `https://svelte.dev/llms-full.txt` |
| SvelteKit only (Kit 3) | `https://svelte.dev/docs/kit/llms.txt` (not stored — the combined file above covers it) | — |
| Better Auth | `https://www.better-auth.com/llms.txt` (short pointer; the docs version is stored) | `https://www.better-auth.com/docs/llms.txt` |
| Drizzle | `https://orm.drizzle.team/llms.txt` | `https://orm.drizzle.team/llms-full.txt` (~3.7 MB, not stored) |
| Vite | `https://vite.dev/llms.txt` | `https://vite.dev/llms-full.txt` |
| Vitest | `https://vitest.dev/llms.txt` | `https://vitest.dev/llms-full.txt` |
| Zod | `https://zod.dev/llms.txt` | — |
| Bits UI | `https://bits-ui.com/llms.txt` | `https://bits-ui.com/docs/llms.txt` |
| Skeleton | `https://skeleton.dev/llms.txt` | `https://skeleton.dev/llms-full.txt` |
| Lucide | `https://lucide.dev/llms.txt` | — |
| Tailwind CSS | — (returns 404; intentionally unpublished) | Hand-built at `tailwindcss/llms.txt` |

## Refreshing the corpus

```sh
cd web/llms
curl -fsSL https://svelte.dev/llms-full.txt        -o sveltekit/llms-full.txt
curl -fsSL https://svelte.dev/llms.txt             -o sveltekit/llms.txt
curl -fsSL https://www.better-auth.com/docs/llms.txt -o betterauth/llms.txt
curl -fsSL https://orm.drizzle.team/llms.txt       -o drizzle/llms.txt
curl -fsSL https://vite.dev/llms-full.txt          -o vite/llms-full.txt
curl -fsSL https://vite.dev/llms.txt               -o vite/llms.txt
curl -fsSL https://vitest.dev/llms-full.txt        -o vitest/llms-full.txt
curl -fsSL https://vitest.dev/llms.txt             -o vitest/llms.txt
curl -fsSL https://zod.dev/llms.txt                -o zod/llms.txt
curl -fsSL https://bits-ui.com/docs/llms.txt       -o bitsui/llms-full.txt
curl -fsSL https://bits-ui.com/llms.txt            -o bitsui/llms.txt
curl -fsSL https://skeleton.dev/llms-full.txt      -o skeletondev/llms-full.txt
curl -fsSL https://skeleton.dev/llms.txt           -o skeletondev/llms.txt
curl -fsSL https://lucide.dev/llms.txt             -o lucide/llms.txt
```

After refreshing, update the snapshot date above and re-check the skill references that point at these files. `tailwindcss/llms.txt` is hand-maintained — update it from the installed Tailwind v4 docs and `web/client/src/routes/layout.css`, not from upstream.

## Recommended additions

Prioritized by how much this monorepo uses the project. Check each docs site for `/llms.txt`.

| Priority | Project | Why it matters here | Docs root to check |
| --- | --- | --- | --- |
| High | TypeScript | strict TS + ESM everywhere; tsconfig and type-level patterns | typescriptlang.org |
| Medium | Playwright | client e2e (`web/client/playwright.config.ts`) | playwright.dev |
| Medium | Node.js | `node:24+` runtime; `node:process`/`node:path`/`node:fs` in configs and server code | nodejs.org |
| Medium | ESLint | flat config + typescript-eslint | eslint.org |
| Medium | postgres.js (porsager/postgres) | client DB driver | porsager/postgres |
| Medium | pg / node-postgres | server DB driver | node-postgres.com |
| Low | ws | WebSocket server | github.com/websockets/ws |
| Low | npm workspaces | monorepo orchestration, scripts, lockfile | docs.npmjs.com |
| Low | Prettier | formatting across the repo | prettier.io |
| Low | mdsvex | client markdown preprocessor | mdsvex.com |

## Usage

1. Create a subdirectory per project and drop the downloaded file(s) in.
2. Skills in [`../../.roo/skills`](../../.roo/skills) reference these files by path in their **References** section.
3. After adding a file, open the relevant skill(s) and tune the steps to match the actual content of the file.
