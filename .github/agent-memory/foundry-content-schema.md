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

**Still dropped by the HTTP path:** child *rows* (recipe ingredients, loot-table entries, actor pools that are tables
rather than JSON) are not copied by `ApplyAuthoredValues` — `ContentPackImporter.ReconcileChildren` is the only code that
writes them, and it matches by authored identity. The PUT endpoint does not load or reconcile children, so editing those
collections through Foundry still round-trips 200 without a write. File a card before promising Foundry edits there.

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
