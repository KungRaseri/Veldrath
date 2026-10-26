# Content Pipeline and Hygiene (Milestone B)

> **Position in the roadmap:** D → C → A → **B**.
> B is last on purpose. It is the milestone that protects everything the other three build, and its shape only
> becomes obvious once party and death content exists. The one exception is the runtime reference check, which
> ships inside Milestone D as #36.
> **Tracker:** epic #39 — B1 #50 (shared validators, one rule set), B2 #51 (Foundry write path + validation),
> B3 #52 (CI content guard), B4 #53 (coverage honesty + changed-lines blocking), B5 #54 (Mailpit, no mail
> bodies in logs, CORS defaults pinned). Epic #39 and all five issues were re-reviewed against the
> post-removal tree on 2026-10-25 and rewritten where the deleted editor changed the work.
>
> **Scope shift (2026-10-25):** `RealmForge` — the Avalonia desktop content editor — has been deleted from the
> repository along with `RealmForge.Tests`, `RealmUI.Fonts`, `RealmForge.slnx`, and `ci-tooling.yml`. Foundry
> inside the SvelteKit app is now **the** content editing surface, so this milestone owns the write path it
> talks to instead of validating saves in a desktop app.

---

## 1. What exists today (verified)

| Thing | State |
|---|---|
| Content storage | Postgres tables (items, powers, actors, quests, recipes, loot tables, zones, map graph, dialogues, …) |
| Authoring | **Foundry only** — the SvelteKit portal under `web/client/src/routes/(foundry)/`. It can browse; it cannot write. The desktop editor that used to write is gone |
| Read API | [`ContentEndpoints`](../Veldrath.Server/Features/Content/ContentEndpoints.cs:53) — `GET` only (plus `/schema`, `/browse`, `/browse/{type}/{slug}`), anonymous |
| Write API | **None.** Every content change today is a SQL statement, a seeder run, or a migration |
| Schema description | [`ContentSchemaRegistry`](../Veldrath.Contracts/Content/ContentSchema.cs:60) — field names/types per entity, powers `/api/content/schema` and the Foundry browse UI |
| Validation | [`MapGraphValidator`](../RealmEngine.Data/Validation/MapGraphValidator.cs:41) only — map graphs, used by the seeder. [`ContentReferenceRules`](../RealmEngine.Data/Validation/Content/ContentReferenceRules.cs) audits references at startup (#36) |
| Everything else | Unvalidated on the way in. A typo'd slug, a negative damage value or a quest pointing at a deleted item lands in the DB and is discovered at play time |

**Two different checks, deliberately separate:**

| Kind | When | Owner | Purpose |
|---|---|---|---|
| Runtime reference check | Server startup | **#36 (Milestone D)** | "The live DB has dangling references" — logged at `Error`, never blocks boot |
| Authoring validation | On write, and in CI over a snapshot | **This milestone** | "This entity is invalid" — blocks the save, prevents the dangling reference from existing at all |

B1 builds the validators; #36 should call the same validators for its reference pass so there is one rule set,
not two.

## 2. Scope

### 2.1 Shared validators for every content family

Reuse the shape that already works — `MapGraphValidationResult` (severity, message, identifier) — and add
validators for: items, powers/abilities, actor archetypes and instances, quests, loot tables, recipes,
zones/locations, dialogues, classes/species/backgrounds/skills.

Each validator covers three layers:

1. **Shape** — required fields present, slug format (`^[a-z0-9]+(-[a-z0-9]+)*$`), enums in range, numbers sane
   (no negative damage, no zero-duration status effect — the `StatusEffectParser` guard we just added is the
   runtime half of this rule).
2. **Semantics** — an ability with `statusEffect` must have a valid effect type and a positive
   `statusDuration`; a loot table must have non-zero weights; a quest must have at least one objective.
3. **References** — every slug it points at exists: `quest.objectives['collect:x']` → item `x`,
   `kill:y` → actor `y`, `visit:z` → location `z`; `zone.location.spawnPool` → actors.

Validator results are `Error` vs `Warning`. Errors block a write; warnings are surfaced but allowed.

### 2.2 Write path and enforcement

Foundry is the only editor, so the server needs a write path. It is deliberately narrow and guarded:

| Endpoint | Purpose |
|---|---|
| `GET /api/content/{type}/{slug}/validate` | Dry run — returns the same `ContentIssue` list without persisting. Lets the editor show problems before the author commits |
| `POST /api/content/{type}` | Create. Rejects `Error`-severity issues with `400` and the issue list in the body |
| `PUT /api/content/{type}/{slug}` | Update. Same rule; keeps the slug immutable |
| `DELETE /api/content/{type}/{slug}` | Soft-delete (the entities already carry the fields) so references can be reported rather than orphaned |

Rules:

- **Authorization:** the existing RBAC claims gate writes (a content-editor permission), while reads stay
  anonymous. Writes go through `ContentDbContext` only — the game's `ApplicationDbContext` is untouched.
- **Validate-then-save, one rule set:** the endpoint calls the B1 validator for the type; `Error` blocks the
  request, `Warning` is returned alongside the saved entity. The same code path answers `validate`, so the dry
  run can never disagree with the real write.
- **Optimistic concurrency:** every content row already has `UpdatedAt`; an `If-Match`-style precondition
  (the `UpdatedAt` the editor loaded) rejects a blind overwrite of someone else's edit.
- **Foundry UI:** edit form rendered from `ContentSchemaRegistry` field metadata, a *Validate* action, and a
  save button that shows the returned issues next to the offending field.
- **CI guard:** a job that runs every validator over the seeded/exported content snapshot and fails on any
  `Error`. This is what stops a content regression from reaching a deployment.

### 2.3 Coverage honesty

- `@veldrath/shared` currently cannot pass its own `coverage` script because it has no tests. Either add the
  tests that make the gate meaningful (schema + generated contracts are worth testing) or remove the script and
  say so — silently failing is not an option.
- Codecov statuses are `informational: true` on purpose. Once the suites are honest, flip them to blocking for
  **changed lines only** so legacy gaps do not block unrelated PRs.

### 2.4 Developer ergonomics

- Mailpit in `docker-compose.dev.yml` so local mail is inspectable and `NullEmailSender` can stop logging
  bodies entirely (the CodeQL hardening in this session already restricted it to Development).
- A `CorsSetup` test that pins the dev defaults, including the preview origin (`http://localhost:4173`) that
  the Playwright game-loop spec needs.

## 3. Phases

| Phase | Work | Done when |
|---|---|---|
| **B1** ✅ | Shared validator library + one validator per content family, with unit tests over good/bad fixtures | Every family has a validator with `Error`/`Warning` output and tests; #36's startup check calls it |
| **B2** | Foundry write path: `validate` / `POST` / `PUT` / `DELETE` on `/api/content`, RBAC-gated, plus the schema-driven Foundry edit form | An invalid entity cannot be saved from Foundry, the reason is shown next to the field, and a valid save round-trips |
| **B3** ✅ | CI content-guard job over the content snapshot | A PR that introduces an invalid entity fails the job and names the entity and field |
| **B4** | Coverage honesty (`@veldrath/shared`), Codecov policy flip for changed lines | `npm run coverage` in `web/shared` passes; Codecov reports a blocking changed-lines status |
| **B5** | Mailpit in the dev stack; `CorsSetup` defaults test; `NullEmailSender` logs no bodies | Local dev shows mail in Mailpit; the CORS preview origin is pinned by a test |

## 4. Acceptance criteria

1. **One rule set:** deleting a reference makes the CI guard, the startup check (#36), and the Foundry write
   path report the same rule and the same identifier.
2. **Blocked write:** saving an item with a dangling `power` reference from Foundry fails with `400` and names
   the field; a valid save succeeds and is visible on the next browse.
3. **No false blocks:** the full seeded content set passes with zero `Error`s (warnings allowed and listed).
4. **Coverage:** `web/shared` reports real coverage and its gate passes; the Codecov changed-lines status is
   blocking.
5. **Mail:** the dev stack delivers to Mailpit, and server logs contain no message bodies.

### B3 outcome (2026-10-26)

`SeededContentValidationTests` now demands **zero** errors over the real seeder — the three defects B1 found were
fixed in content, not tolerated: `glowcap-mushroom` and `lost-cargo` are real quest items now, `the-lost-shipment`
uses the implemented `talk:` kind instead of an invented `interact:`, and `arcane-focus` carries `ManaCost = 0`
instead of a negative cost that would have refunded mana if the passive were ever cast.

That last one exposed the hole that let `interact:` through: the audit skipped objective kinds it did not recognise,
so a typo produced an objective that could never progress and nothing said so. Unknown kinds are now reported as
`unsupported-quest-kind` alongside the reserved `deliver:` kind, and `talk:` is explicitly recognised as
implemented-but-unverifiable (there is no NPC catalog yet).

There is no dedicated workflow for this, and there should not be: a `content-guard.yml` was written first and then
deleted as redundant. [`ci-server.yml`](../.github/workflows/ci-server.yml) already runs **all** of
`Veldrath.Server.Tests` whenever `RealmEngine.Data/**` or `Veldrath.Server/**` changes, which is exactly when content
or a rule can change, and `SeededContentValidationTests` is part of that suite. A second workflow running one test
class behind a narrower `paths` filter added latency and no coverage.

### B1 outcome (2026-10-26)

`RealmEngine.Data/Validation/Content/` ships `ContentIssue`/`ContentValidationResult`, `ContentRules` (stable
rule ids), `ContentReferenceCatalog`, `IContentValidator` + `ContentValidator<T>`, one validator per family and
`ContentValidatorRegistry` keyed by the same type keys `ContentSchemaRegistry` advertises. `ContentReferenceRules`
and `QuestObjectiveKinds` moved into shared code, so the startup audit, the B2 write path and the B3 guard all
call one implementation.

Running the validators over the real seeded catalog found **three** content defects, now asserted by name in
`SeededContentValidationTests` so they cannot be forgotten: the two dangling quest objectives
(`the-lost-shipment` → `collect:lost-cargo`, `gather-mushrooms` → `collect:glowcap-mushroom`) and
`arcane-focus` carrying a negative mana cost. B3 fixes all three and then the allow-list shrinks to nothing.

## 5. Explicitly deferred

- Bulk import/export of a whole content family (single-entity CRUD only for now).
- Generated TypeScript types from the C# contracts — the payload and content types stay hand-mirrored until
  someone proves generation is cheaper than the drift test.
- Per-entity versioning/audit history for content changes (the `UpdatedAt` precondition is the whole
  concurrency story for this milestone).
- A visual map-graph editor in Foundry — the map graph is authored as data through the same write path.
