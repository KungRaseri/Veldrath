# RealmEngine — Forge ↔ Foundry Sync & Server Implementation Notes

## ContentSchema Foundation (March 2026)

### `ContentSchema.cs` (`RealmUnbound.Contracts/Content/`)
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

## RealmFoundryApiClient Additions
- `GetContentTypesAsync()` → schema list
- `BrowseContentAsync(type, search, page, pageSize)` → `PagedResult<ContentSummaryDto>`
- `GetContentDetailAsync(type, slug)` → `ContentDetailDto?`

## Foundry UI Changes
- `NewSubmission.razor` — replaces raw JSON textarea with schema-driven form groups (all 20 types); `BuildPayloadJson()` converts flat dot-path dict → nested JSON
- `NavMenu.razor` — "Content" nav link → `/content`
- `ContentBrowser.razor` (`/content`) — grid of all 20 content types
- `ContentDetail.razor` (`/content/{ContentType}[/{Slug}]`) — list pane + detail pane; schema groups drive field rendering of the `JsonElement` payload; debounced search, pagination (25/page)

## RealmUnbound Server Fixes (March 2026)
- `GameEngineHealthCheck` injects `ISender` (MediatR) so DI-wiring is verified at health check time
- `FoundryEndpoints.ListAsync` uses `int? page, int? pageSize` (non-nullable value-type query params return 400 in Minimal APIs)
- `FoundryEndpoints` rate-limit reads from `RateLimit:FoundryWritesPerMinute` config (default 5 prod, 100000 test)
- `ApplicationDbContext.OnModelCreating` applies `DateTimeOffsetToStringConverter` to all `DateTimeOffset` properties on SQLite so ORDER BY works in integration tests
- `ApplicationDbContext.FoundrySubmission.Payload` is `TEXT` (not `jsonb`) on SQLite to avoid affinity issues
- `RealmUnbound.Server.Tests.csproj` references Moq and has global usings for `RealmUnbound.Contracts.Foundry`, `Moq`, `MediatR`

## RealmUnbound Server + Client Iteration (2026-03-19)

### Completed this session
- Fixed pre-existing build error: `ActorClassDto` gained `HitDie`, `PrimaryStat`, `RarityWeight` params; updated `FakeServices.cs` and 2 `CharacterSelectViewModelTests.cs` direct ctor calls
- Fixed `AvailableClasses_Should_Contain_Expected_Classes` test — renamed to `AvailableClasses_Should_Contain_Expected_Classes_When_Catalog_Empty`; added `await Task.Delay(50)` and empty catalog to correctly test fallback path
- Fixed P3 stub: `MainMenuViewModel.SettingsCommand` now navigates to new `SettingsViewModel`; test updated to check navigation
- Created `SettingsViewModel` (`RealmUnbound.Client/ViewModels/SettingsViewModel.cs`) with `BackCommand` to return to main menu
- Implemented `AllocateAttributePoints` Hub→MediatR bridge:
  - `RealmUnbound.Server/Features/Characters/AllocateAttributePointsHubCommand.cs` — command + result + handler (reads/writes `Character.Attributes` JSON blob)
  - `GameHub.AllocateAttributePoints(Dictionary<string,int>)` — validates ownership via `TryGetCharacterId`, dispatches, broadcasts zone-group or caller
- Added 9 tests for `AllocateAttributePoints` in `GameHubTests.cs` (hub dispatch, handler direct, success/error/not-in-zone/in-zone paths)

### Test counts after this session
- RealmUnbound.Client.Tests: 212 passing
- RealmUnbound.Server.Tests: 226 passing

### Remaining open items (P1/P2/P3/P4)
- P4 XML doc gaps: ~55 missing `<summary>` on public DTOs/interfaces (see engine-codebase.md for list). This will produce CS1591 build errors when those files are next touched.
- P2: SelectCommand concurrent retry logic not tested; DeleteCommand error recovery not exhaustively tested
- Next hub→MediatR candidates: `UseAbility`, `EquipItem`, `CraftItem` (all need server-side entity model and handler to be created first)
- `SettingsViewModel` is a placeholder — full settings screen (audio, server URL config, keybindings) is future work

## WebAppFactory / Integration Test Gotcha
- Curator role is NOT created at startup in SQLite/Test env — role creation in `Program.cs` is inside `if (providerName.Contains("Npgsql"))`
- `GetCuratorTokenAsync` helper must: register → `CreateScope()` → `RoleManager.CreateAsync("Curator")` → `UserManager.AddToRoleAsync` → **re-login** (re-login required because `IssueTokenPairAsync` calls `IsInRoleAsync`; old token won't have the Curator role claim)

## FoundryEndpointTests Coverage (March 2026)
29 integration tests covering all 7 Foundry endpoints: Create (201/401/400/Location header), List (paged/filter by type/status/search), Get (full detail/404), Vote (up/down/401/invalid/change), Review (approve/reject/403/401/400/notifications), Notifications (empty/401/mark-read/404 unknown/404 other user).
