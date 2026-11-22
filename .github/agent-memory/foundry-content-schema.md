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
table, so it was never affected. `LootTable` with no entries is an `Error` in the validator, so an approval that omits
`entries` is refused before the reconcile rather than silently deleting them; a recipe with no ingredients is only a
warning.

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
`FoundryEndpointTests.Review_Should_Persist_An_Authored_Ingredient_Edit_On_Approval`.

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
