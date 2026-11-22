# RealmEngine — Foundry Content Schema Notes

> **Status**: Reviewed and current as of Session-39 (2026-06-27). The RealmForge desktop editor was
> removed in Milestone B; Foundry (the SvelteKit portal under `web/client/src/routes/(foundry)/`) is
> the only content editing surface.

> For Veldrath Server + Client iteration history, see [veldrath-memory.md](veldrath-memory.md).

## ContentSchema Foundation (March 2026)

### `ContentSchema.cs` (`Veldrath.Contracts/Content/`)
- Defines `ContentFieldType`, `ContentFieldDescriptor`, `ContentFieldGroup`, `ContentTypeSchema`
- `ContentSchemaRegistry` static class: maps 20 type keys → schemas
- Type keys: ability, species, class, archetype, instance, background, skill, weapon, armor, item, material, materialproperty, enchantment, spell, quest, recipe, loottable, organization, worldlocation, dialogue
- Field names use dot-paths matching camelCase JSON serialization of EF Core owned types (e.g. `stats.manaCost`, `traits.isAoe`)

### `ContentContracts.cs` Additions
- `ContentSummaryDto` — paged list row (Id, Slug, DisplayName, TypeKey, ContentType, RarityWeight, IsActive, UpdatedAt)
- `ContentDetailDto` — summary + `JsonElement Payload` (full entity camelCase JSON)
- `ContentTypeInfoDto` — type catalog entry (ContentType, DisplayLabel, Description)

## Server Endpoints (`ContentEndpoints.cs`)
- Content group changed from `RequireAuthorization()` → `AllowAnonymous()`
- `GET /api/content/schema` — returns all `ContentTypeInfoDto`
- `GET /api/content/browse?type=&search=&page=&pageSize=` → `PagedResult<ContentSummaryDto>`
- `GET /api/content/browse/{type}/{slug}` → `ContentDetailDto` (full entity as JSON)
- Browse uses `BrowseSet<T>` generic helper with `EF.Functions.ILike` search on ContentDbContext
- Type aliases `SharedAbility/Quest/Recipe/Spell` resolve ambiguity with RealmEngine.Shared.Models

## Web client wrappers (`web/client/src/lib/server/content-api.ts`)
- `listContentTypes()` → schema list
- `browseContent(type, search, page, pageSize)` → `PagedResult<ContentSummaryDto>`
- `getContentDetail(type, slug)` → `ContentDetailDto`
- `GetContentDetailAsync(type, slug)` → `ContentDetailDto?`

## Foundry UI Changes
- `NewSubmission.razor` — replaces raw JSON textarea with schema-driven form groups (all 20 types); `BuildPayloadJson()` converts flat dot-path dict → nested JSON
- `NavMenu.razor` — "Content" nav link → `/content`
- `ContentBrowser.razor` (`/content`) — grid of all 20 content types
- `ContentDetail.razor` (`/content/{ContentType}[/{Slug}]`) — list pane + detail pane; schema groups drive field rendering of the `JsonElement` payload; debounced search, pagination (25/page)

## Veldrath Server Fixes (March 2026)
- `GameEngineHealthCheck` injects `ISender` (MediatR) so DI-wiring is verified at health check time
- `FoundryEndpoints.ListAsync` uses `int? page, int? pageSize` (non-nullable value-type query params return 400 in Minimal APIs)
- `FoundryEndpoints` rate-limit reads from `RateLimit:FoundryWritesPerMinute` config (default 5 prod, 100000 test)
- `ApplicationDbContext.OnModelCreating` applies `DateTimeOffsetToStringConverter` to all `DateTimeOffset` properties on SQLite so ORDER BY works in integration tests
- `ApplicationDbContext.FoundrySubmission.Payload` is `TEXT` (not `jsonb`) on SQLite to avoid affinity issues
- `Veldrath.Server.Tests.csproj` references Moq and has global usings for `Veldrath.Contracts.Foundry`, `Moq`, `MediatR`

## WebAppFactory / Integration Test Gotcha
- Curator role is NOT created at startup in SQLite/Test env — role creation in `Program.cs` is inside `if (providerName.Contains("Npgsql"))`
- `GetCuratorTokenAsync` helper must: register → `CreateScope()` → `RoleManager.CreateAsync("Curator")` → `UserManager.AddToRoleAsync` → **re-login** (re-login required because `IssueTokenPairAsync` calls `IsInRoleAsync`; old token won't have the Curator role claim)

## FoundryEndpointTests Coverage (March 2026)
29 integration tests covering all 7 Foundry endpoints: Create (201/401/400/Location header), List (paged/filter by type/status/search), Get (full detail/404), Vote (up/down/401/invalid/change), Review (approve/reject/403/401/400/notifications), Notifications (empty/401/mark-read/404 unknown/404 other user).

## Replacing a content row: owned JSON has to be copied in place

`ContentValueCopy.ApplyAuthoredValues` ([`RealmEngine.Data/Content/ContentValueCopy.cs`](../../RealmEngine.Data/Content/ContentValueCopy.cs))
is the only supported way to write an authored entity over a stored one. `Entry(existing).CurrentValues.SetValues(incoming)`
walks the owner's **scalar columns only**: owned JSON navigations (`Item.Stats`/`Traits`, quest `Objectives`, a language's
`Phonology`/`Morphology`/`RegisterSystem`) are separate tracked entries, so `SetValues` left them at their stored values
while the write returned 200 and bumped `Version` — a silent no-op for those fields (defect found 2026-11-20; the PUT
endpoint and the Foundry review-approval path both had it). Owned values must be mutated in place (`CopyOwnedInPlace`),
never replaced with a new owned instance on a tracked row. All three write paths — `ContentWriteEndpoints.UpdateAsync`,
`ContentWriteService.PromoteAsync` and `ContentPackImporter` — call the shared helper so they cannot diverge again.
Regression tests: `ContentWriteEndpointTests.An_owned_json_edit_on_a_language_is_persisted`,
`..._on_an_item_is_persisted`, `Removing_an_entry_from_an_owned_json_collection_is_persisted`, and
`FoundryEndpointTests.Review_Should_Persist_An_Owned_Json_Correction_On_Approval`.

**A partial payload must not be a delete.** Copying the owned value wholesale turned the old silent no-op into a
silent deletion for the one client that sends partial payloads: Foundry's submission forms render the *schema*, and a
schema exposes only part of an entity (the language schema has four of the seven `phonology` lists and no `morphology`
or `registerSystem`), so a schema-shaped correction omitted everything else and approval wrote the deserialiser's
empty defaults. Two guards now hold:

- `ContentValueCopy.FillUnnamedOwnedValues` (approval path, `ContentWriteService.PromoteAsync`) fills owned-JSON
  values the payload does not name from the stored row before validation and application. Anything the payload *does*
  name still replaces — an empty list clears, so full-replace semantics survive for what the caller wrote.
- The two Foundry submission actions (`(foundry)/submissions/[id]` create, `(foundry)/languages/new`) seed the
  payload from `GET /api/content/browse/{type}/{slug}` when the slug already exists, so the stored submission is
  honest and the review diff does not claim a deletion. The generic content editor already did this
  (`buildPayload(detail.data.payload, …)`).
Regression test: `FoundryEndpointTests.Review_Should_Preserve_Owned_Json_A_Schema_Shaped_Submission_Does_Not_Name`.

**A seeded trait must not survive a blanked sibling (fixed 2026-11-20, card t_b9bc2752).** Seeding the payload base is
not enough on its own: the submission form renders *every* scalar field of the schema and posts them all, so the fields
the author did not fill arrive empty and `buildPayload` wrote those blanks over the stored row. A seeded
`traits.hasCooldown: true` then stood beside a blanked `stats.cooldown`, and `ContentWriteService.PromoteAsync` refused
the payload ("flagged as having a cooldown but declares none") — approval never landed and the review page kept showing
the alert instead of "Approved". This had been masked before card t_3e4f919a because `original` was `{}`, so no trait
came along; the seeding exposed it. `submissionPayload`
([`web/client/src/lib/foundry/content-editor.ts`](../../web/client/src/lib/foundry/content-editor.ts)) now writes only
the fields whose submitted value differs from the one the form rendered (the schema default, or `''` for a select);
a submission with no stored row still writes every submitted field. What that costs, and what to remember: on a
correction the create form cannot express "set it back to the schema default", cannot turn a Boolean *off* (an unchecked
checkbox submits nothing), and has no input at all for `StringList`/`KeyValue`/`ObjectList` — all of those keep their
stored values. Regression tests: `content-editor.test.ts` (`submissionPayload` cases) and `foundry-loaders.test.ts`
`keeps a stored stat the form posted blank, so a seeded trait is never left without it`.

**Child rows are reconciled, not copied (fixed 2026-11-20, card t_3e4f919a):** child *rows* (recipe ingredients,
loot-table entries) cannot be assigned to the tracked parent — a deserialised child arrives with its key set, so EF
issues an UPDATE against a row that was never inserted. They are now written by `ContentChildRows`
([`RealmEngine.Data/Content/ContentChildRows.cs`](../../RealmEngine.Data/Content/ContentChildRows.cs)), which the
importer, the PUT endpoint and the approval path all share: matches are mutated in place, dropped rows deleted and
additions inserted, matching on authored identity (an ingredient's `ItemDomain`+`ItemSlug`) or, for a surrogate-keyed
child such as `LootTableEntry`, on its authored values.

**Presence, not emptiness, decides deletion.** Each write path passes the collection names the payload actually
names (`ContentChildRows.ApplyNamedAsync` → `NamedIn`), so a PUT or submission that omits `ingredients` leaves the
stored ingredients alone instead of zeroing them. A collection the payload *does* name is replaced wholesale, so an
empty list still clears it. `ZoneLocation.ActorPool` is owned JSON (`OwnsMany(...).ToJson("ActorPool")`), not a child
table, so the reconcile never touches it — it is assigned wholesale by `ContentValueCopy.ApplyAuthoredValues`, which is
why both the null rule below and the owned-value fill have to cover it. That fill
(`ContentValueCopy.FillUnnamedOwnedValues`) runs in the **update** PUT and the dry run, not just in the
review-queue approval (a create has no stored row to fill from): an omitted owned collection deserialises to `[]`
and an omitted owned object to a fresh instance, so without it "omitted" and "`[]`" would both wipe an owned value and
the `child_collection_null` message
("omit the property to leave the stored collection alone") would be advising the delete it refuses. `LootTable` with no
entries is an `Error` in the validator, so an approval that omits `entries` is refused before the reconcile rather than
silently deleting them; a recipe with no ingredients is only a warning.

**An explicit `null` child collection is refused, not read as empty (fixed 2026-11-20, card t_cb4c17ef).** Three
shapes, three meanings, and only two of them are authoring: **omitted** = leave the stored rows alone, **`[]`** =
replace them with nothing, **`null`** = neither — the payload names the property and gives it no value, so it is a
shape error. `ContentChildRows.NamedIn` now counts only a property that carries a JSON **array** as naming a
collection, and `ContentChildRows.Reconcile` skips a null collection even when the caller asks for every collection
(the pack importer does). The write path refuses it first with rule `child_collection_null` naming the field:

```text
PUT /api/content/recipe/{slug}         {"…": "…", "ingredients": null}   -> 400, errors[].field = "ingredients"
PUT /api/content/loottable/{slug}      {"…": "…", "entries": null}       -> 400, errors[].field = "entries"
PUT /api/content/species/{slug}        {"…": "…", "powerPool": null}     -> 400, errors[].field = "powerPool"
PUT /api/content/power/{slug}          {"…": "…", "speciesPool": null}   -> 400, errors[].field = "speciesPool"
PUT /api/content/zonelocation/{slug}   {"…": "…", "actorPool": null}     -> 400, errors[].field = "actorPool"
PUT /api/content/language/{slug}       {"…": "…", "phonology": {"consonants": null}}
                                                                        -> 400, errors[].field = "phonology.consonants"
```

`power` is the read alias of the `ability` family and had **no alias entry and no validator of its own**, so every
write through `/api/content/power/{slug}` threw `KeyNotFoundException` → 500 (pre-existing, not caused by the null
rule; the null payload just made it visible). `Aliases["power"] = "ability"` plus `TryResolve` folding an
unregistered key onto `CanonicalFamily` fix the route, and
`ContentEntityTypesTests.Every_known_type_key_resolves_to_a_registered_validator` now forbids a map entry whose family
has no validator.

The rule has to be model-driven (`ContentChildRows.NullCollectionIssues`) rather than per-validator: `species`,
`class`, `archetype`, `instance` and `ability` have no rule that reads their pools, so a null reached the reconcile
and **cleared the pool on a 200** — a silent delete, not the 500 the recipe path produced. All six validation call
sites in three files go through `ContentEntityTypes.Validate(resolver, entity, model, catalog)`, which merges the shape
issues ahead of the family validator's: `ContentWriteEndpoints` (create, update and both validate routes),
`ContentWriteService.PromoteAsync` and `ContentPackValidator.ValidateAsync`. The family validators are also null-tolerant
(`?? []`) so the offline pack validator and the CI guard can never throw a `NullReferenceException` on a null
child-row or owned collection the shape rule already refused. That tolerance does **not** close the class on its own:
a per-validator `?? []` only helps the families whose validator happens to read the list, and it is why the shape rule
now walks **one level into an owned object** (below).

`"objectives": {"items": null}` on a quest deserialised `Quest.Objectives.Items` null, and `QuestValidator` read it
unguarded: every quest write and the pack validator answered 500 (first frame `QuestValidator.ValidateEntity`). It now
reads `entity.Objectives?.Items ?? []`, so the same payload is the ordinary `quest_without_objectives` error (400 on
the write routes) *and* `child_collection_null` on `objectives.items` with the stored row and version intact
(2026-11-22, card t_cb4c17ef).

The same rule covers an **owned** collection. `ZoneLocation.ActorPool` is owned JSON, so no reconcile sees it:
`ContentValueCopy.ApplyAuthoredValues` assigns it wholesale and a null would delete the pool on a 200 (the 2026-11-21
review measured exactly that — `200 isValid:true` with `spawn_pool_empty` as a warning and the reload empty). Owned
collections are therefore in `NullCollectionIssues` too, and `ApplyAuthoredValues` skips a null owned collection as
defence in depth (as `CopyOwnedInPlace` now skips a null collection nested inside an owned object — `Phonology.Consonants`,
`Objectives.Items`, `Stats.Lines`). A stored location loads its pool as `[]`, never null, so
`GET /api/content/zonelocation/{slug}/validate` stays clean — the rule only fires on a payload that names the field as
null.

**The walk goes one level into an owned object (fixed 2026-11-22, card t_cb4c17ef).** All eleven of a language's lists
live inside owned JSON (`Phonology.Consonants`/`Vowels`/`AllowedSyllablePatterns`/…, `Morphology.Roots`/`Prefixes`/
`Suffixes`, `RegisterSystem.Registers`), so **no entity navigation carries them** and `NullCollectionIssues` returned 0
for them; `language` runs the structural validator, which reads none of them, so no per-validator `?? []` could either.
Measured before the walk existed: `PUT /api/content/language/calethic` with `"phonology": {"consonants": null}` answered
**200 `isValid:true` version 2**, stored the null, and then **both `GET /api/content/languages` and
`GET /api/content/languages/{slug}` answered 500** (`EfCoreLanguageRepository.MapToModel` dereferences
`e.Phonology.Consonants`, so the 500 is the proof the null reached the column) — the whole language catalog down until
the row was repaired. `NullCollectionIssues` now reports each null collection inside an owned object with its dotted
field (`phonology.consonants`, `objectives.items`, `rewards.items`, `stats.lines`, `stats.options`), for owned
collections (`OwnsMany`) and primitive collections (`PrimitiveCollection`) alike, so the payload is refused with
`child_collection_null` and the row and both reads survive. A null **owned object** (`"stats": null`) is still a
different defect and belongs to card t_819ada90 — the walk reports collections, not the object itself, and
`ApplyAuthoredValues` has no guard for a null owned object.

The three shapes are only three shapes because the update PUT and the dry run fill an unnamed owned value from
the row being replaced (`ContentValueCopy.FillUnnamedOwnedValues`, the same call `ContentWriteService.PromoteAsync` has
always made) before deserialising; a create has no stored row to fill from. `2026-11-22` review measured the gap that
left: `PUT /zonelocation` **omitting**
`actorPool` deserialised to `[]`, was assigned wholesale and answered `200` with the pool deleted, while the 400 above
told the author to omit the field. The PUT now keeps the pool (`Version` bumped once), `"actorPool": []` still clears
it, an omitted owned **object** keeps its stored leaves (`stats.dangerLevel` 7 survives), and the payload dry run reports
no spurious `spawn_pool_empty` for a pool the write keeps. That also makes the "omitted = keep stored" premise of the
explicit-null-owned-object card (t_819ada90) true on this route.

**The read path has to return the child rows, or the presence rule deletes them.** Foundry's editor and the submission
forms build their payload from `GET /api/content/browse/{type}/{slug}`, and that read used `AsNoTracking()` with no
`Include`, so every child collection serialised as `[]` — the payload then *named* `ingredients: []`, and presence
turned one Save into a silent delete of every stored ingredient (the first cut of this fix shipped that defect;
reviewer probe 2026-11-20 caught it). `BrowseDetailAsync` now goes through
`ContentChildRows.IncludeAuthoredChildren(...)`, which enumerates the model's authored child navigations, so the detail
payload is exactly what the write path reconciles. `_detailJsonOpts` uses `ReferenceHandler.IgnoreCycles` because a
loaded child's navigation back to its parent is a cycle. A payload that carries a child's surrogate `id` gets a fresh
one on insert (`ContentChildRows.AddChild` clears generated keys): the replaced row is deleted in the same save, and
reusing its key would collide.

Regression tests: `ContentWriteEndpointTests.An_ingredient_quantity_edit_through_put_is_persisted`,
`Adding_and_removing_ingredients_through_put_stores_exactly_the_authored_set`,
`A_put_that_omits_ingredients_leaves_the_stored_children_alone`, `A_loot_entry_edit_through_put_is_persisted`,
`Adding_and_removing_loot_entries_through_put_stores_exactly_the_authored_set`, the detail round trips
`The_recipe_detail_payload_carries_the_stored_ingredients`,
`A_recipe_saved_from_its_detail_payload_keeps_its_ingredients`,
`A_loot_table_saved_from_its_detail_payload_keeps_its_entries`,
`A_loot_entry_edited_through_the_detail_payload_is_persisted`, and
`FoundryEndpointTests.Review_Should_Persist_An_Authored_Ingredient_Edit_On_Approval`. The null-collection contract is
pinned by `ContentWriteEndpointTests.A_put_naming_ingredients_as_null_is_refused_and_keeps_the_stored_rows`,
`A_put_naming_entries_as_null_is_refused_and_keeps_the_stored_rows`,
`A_put_naming_a_species_power_pool_as_null_is_refused_and_keeps_it`,
`A_put_through_the_power_read_alias_refuses_a_null_pool_instead_of_failing`,
`A_put_naming_the_zone_location_spawn_pool_as_null_is_refused_and_keeps_it`,
`A_put_naming_a_nested_quest_objective_list_as_null_is_refused_and_keeps_the_stored_objectives`,
`A_put_naming_a_language_phonology_list_as_null_is_refused_and_keeps_the_catalog_readable`,
`Validating_a_payload_with_a_null_collection_reports_the_issue_instead_of_failing`,
`ContentChildRowsTests.ApplyNamedAsync_leaves_a_collection_the_payload_names_as_null_alone`,
`Reconcile_leaves_a_collection_the_entity_carries_as_null_alone`,
`NullCollectionIssues_names_an_owned_collection_an_entity_carries_as_null`,
`NullCollectionIssues_names_a_collection_nested_inside_an_owned_object` (with its primitive-collection and
same-leaf-name quest cases),
`ContentValueCopyTests.ApplyAuthoredValues_leaves_an_owned_collection_carried_as_null_alone` (and its replace-when-authored
counterpart, plus the nested-collection null/empty pair), `NullChildCollectionValidationTests` (the rule for the seven
child-row families, the owned `ZoneLocation.ActorPool`, and the lists nested inside owned objects) and
`ContentEntityTypesTests` (every type key resolves to a validator; `power` folds onto
`ability`).

## Dialogue options (2026-11-20)
`Dialogue` stops being only a line pool: `DialogueStats.Options` (owned, `List<DialogueOption>`) carries the choices a
conversation offers. Authored in the Foundry **Dialogues** form under a new **Options** group. Empty options means the
entry is *spoken*, not answered — every dialogue authored before this keeps working unchanged.

Each option: `key` (stable id the client sends back), `label` (≤ 80 chars), `response` (what the speaker says),
`nextSlug` (another dialogue entry, or blank to end), `oneShot` (blank = "once, if it grants anything"), and two text
blocks parsed by [`DialogueOptionGrammar`](../../RealmEngine.Data/Content/DialogueOptionGrammar.cs):

```text
requires:  level:3 | item:slug:2 | quest:active:slug | quest:complete:slug | objective:talk:slug:1
grants:    item:slug:1 | gold:25 | quest-start:slug | objective:talk:slug:1
```

- **One string per list, not nested objects.** A nested list of lists is neither editable in the Foundry form nor worth
  fighting the owned-JSON mapping for; the grammar is the contract, and the validator and the runtime parse it with the
  same code.
- **No migration was needed.** Options ride inside the existing `Stats` JSON column (`OwnsMany` inside `ToJson()`), so
  `dotnet ef migrations has-pending-model-changes` still reports nothing. The persistence test
  [`DialogueOptionPersistenceTests`](../../RealmEngine.Data.Tests/Persistence/DialogueOptionPersistenceTests.cs) uses
  **SQLite** because InMemory keeps object references and cannot prove a value reached the database.
- **Rules** (`ContentRules`): `dialogue_option_invalid` (key/label/response, duplicate key, > 8 options, self-chaining),
  `dialogue_effect_invalid` (unreadable or misplaced entry), `missing-dialogue-reference` (item/quest/dialogue the option
  names does not exist), `dialogue_repeatable_grant` (warning: grants but marked repeatable — a money printer).
- **The content is not seeded.** The three townspeople keep their greeting lines; their conversations are authored in
  Foundry on the existing entries. Seeders only insert by slug, so a seeded conversation would never update.
