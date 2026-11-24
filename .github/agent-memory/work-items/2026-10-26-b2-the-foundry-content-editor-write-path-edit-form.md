---
kind: work-item
date: 2026-10-26
area: web
title: "B2: the Foundry content editor (write path + edit form)"
---

### Session-92 (2026-10-26) — B2: the Foundry content editor (write path + edit form)

**Server (committed first, `ca3997b7`):** `GET /{type}/{slug}/validate`, `POST /{type}`,
`PUT /{type}/{slug}`, `DELETE /{type}/{slug}` behind `Permissions.ManageContent`. `PUT` takes
`expectedUpdatedAt` (409 when stale), keeps `Id`, and stamps `UpdatedAt`/`Version` itself.
`DELETE` deactivates (`IsActive = false`) — never a hard delete, because live game rows reference
content by slug. `ContentEntityTypes` maps a type key to the CLR type + DbSet by reflection over the
context's typed sets.

**Web:** per-type `GET /api/content/{type}/schema` (public, from `ContentSchemaRegistry`) plus a
`POST /{type}/{slug}/validate` that validates an *unsaved payload*; the GET version only re-reports
what is stored, which is why the POST was needed for the Validate button to be honest.
`web/client/src/lib/foundry/content-editor.ts` holds the pure form logic (dot-path read/write, value
coercion, `issuesByField`, `buildPayload`, `initialValues`) and the edit page
`(foundry)/content/[type]/[slug]/edit` is entirely schema-driven.

**Traps**
- `$state(initialValues(data.payload, data.schema))` trips `svelte/state_referenced_locally`; the
  documented escape is `$state(untrack(() => …))`. Do that instead of disabling the warning.
- `bind:checked={values[name]}` on a `Record<string, string | boolean>` does not typecheck; use
  `checked={…} onchange={(e) => …}` (same for `select`/`textarea` via `toInputValue()`).
- `GET /api/content/{type}/schema` shadows a `/{type}/{slug}`-shaped literal: content whose slug is
  literally `schema` is unreachable through `/powers/schema`. Acceptable, but remember it.
- `EF.Functions.ILike` is Npgsql-only, so any test that exercises `?search=` against the SQLite test
  host fails at translation; assert on a plain browse page instead.

**Tests:** `ContentWriteEndpointTests` (14) + `ContentEditorEndpointTests` (4) on the server;
`content-editor.test.ts` (21 cases) in Vitest. Server 814 → **832**, full solution **3,986**;
web typecheck 0 errors/warnings, lint clean, Vitest 11 files, build OK.
