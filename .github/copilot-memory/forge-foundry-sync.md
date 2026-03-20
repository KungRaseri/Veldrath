# RealmEngine — Forge ↔ Foundry Sync Notes

> For RealmUnbound Server + Client iteration history, see [unbound-memory.md](unbound-memory.md).

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

## WebAppFactory / Integration Test Gotcha
- Curator role is NOT created at startup in SQLite/Test env — role creation in `Program.cs` is inside `if (providerName.Contains("Npgsql"))`
- `GetCuratorTokenAsync` helper must: register → `CreateScope()` → `RoleManager.CreateAsync("Curator")` → `UserManager.AddToRoleAsync` → **re-login** (re-login required because `IssueTokenPairAsync` calls `IsInRoleAsync`; old token won't have the Curator role claim)

## FoundryEndpointTests Coverage (March 2026)
29 integration tests covering all 7 Foundry endpoints: Create (201/401/400/Location header), List (paged/filter by type/status/search), Get (full detail/404), Vote (up/down/401/invalid/change), Review (approve/reject/403/401/400/notifications), Notifications (empty/401/mark-read/404 unknown/404 other user).
