# Content Pipeline and Hygiene (Milestone B)

> **Position in the roadmap:** D → C → A → **B**.
> B is last on purpose. It is the milestone that protects everything the other three build, and its shape only
> becomes obvious once party and death content exists. The one exception is the runtime reference check, which
> ships inside Milestone D as #36.
> **Tracker:** epic #39 — phases B1–B5 become issues when the milestone starts.

---

## 1. What exists today (verified)

| Thing | State |
|---|---|
| Content storage | Postgres tables (items, powers, actors, quests, recipes, loot tables, zones, map graph, dialogues, …) |
| Authoring | RealmForge (Avalonia, full CRUD) and the SvelteKit Foundry portal (browse, read-only) |
| Read API | [`ContentEndpoints`](../Veldrath.Server/Features/Content/ContentEndpoints.cs:53) — `GET` only, plus `/schema` and `/browse` |
| Schema description | [`ContentSchemaRegistry`](../Veldrath.Contracts/Content/ContentSchema.cs:60) — field names/types per entity, powers `/api/content/schema` and the Foundry UI |
| Validation | [`MapGraphValidator`](../RealmEngine.Data/Validation/MapGraphValidator.cs:41) only — map graphs, used by RealmForge and the seeder |
| Everything else | Unvalidated. A typo'd slug, a negative damage value or a quest pointing at a deleted item lands in the DB and is discovered at play time |

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

### 2.2 Write-path enforcement

- **RealmForge**: validate before save, show the issues in the editor (the map editor already does this, so
  the pattern and the UX exist), block saving on `Error`.
- **Foundry portal**: read-only validation state via `GET /api/content/{type}/{slug}/validate` using the same
  validator and issue shape, so authors can see problems without opening RealmForge.
- **CI guard**: a job that runs every validator over the seeded/exported content snapshot and fails on any
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
| **B1** | Shared validator library + one validator per content family, with unit tests over good/bad fixtures | Every family has a validator with `Error`/`Warning` output and tests; #36's startup check calls it |
| **B2** | RealmForge write-path validation + Foundry `validate` endpoint | An invalid entity cannot be saved in RealmForge; Foundry reports the same issues read-only |
| **B3** | CI content-guard job over the content snapshot | A PR that introduces an invalid entity fails the job and names the entity and field |
| **B4** | Coverage honesty (`@veldrath/shared`), Codecov policy flip for changed lines | `npm run coverage` in `web/shared` passes; Codecov reports a blocking changed-lines status |
| **B5** | Mailpit in the dev stack; `CorsSetup` defaults test; `NullEmailSender` logs no bodies | Local dev shows mail in Mailpit; the CORS preview origin is pinned by a test |

## 4. Acceptance criteria

1. **One rule set:** deleting a reference makes both the CI guard and the startup check (#36) report the same
   rule and the same identifier.
2. **Blocked write:** RealmForge refuses to save an item with a dangling `power` reference, showing the reason.
3. **No false blocks:** the full seeded content set passes with zero `Error`s (warnings allowed and listed).
4. **Coverage:** `web/shared` reports real coverage and its gate passes; the Codecov changed-lines status is
   blocking.
5. **Mail:** the dev stack delivers to Mailpit, and server logs contain no message bodies.

## 5. Explicitly deferred

- A web-based full CRUD editor for content (Foundry stays read-only; RealmForge is the editor).
- Generated TypeScript types from the C# contracts — the payload and content types stay hand-mirrored until
  someone proves generation is cheaper than the drift test.
- Per-entity versioning/audit history for content changes.
