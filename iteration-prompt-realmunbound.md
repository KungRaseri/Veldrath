Continue iterating on the RealmUnbound projects (RealmUnbound.Server, RealmUnbound.Client, and RealmUnbound.Contracts).

Setup:
- Read `.github/copilot-memory/unbound-memory.md` **before writing any code**. It contains architecture rules, blob schema, DI gotchas, and the full session log.
- Run `dotnet test RealmUnbound.slnx --filter Category!=UI` to establish the baseline. All work must finish with zero regressions against that baseline.
- Baseline (session-12, 2026-03-20): Client=308, Server=307, Total=615.

Completed — do not re-implement:
- **Hub bridges (8 total, all fully wired end-to-end):** `GainExperience`, `RestAtLocation`, `AllocateAttributePoints`, `UseAbility`, `AwardSkillXp`, `EquipItem`, `AddGold`, `TakeDamage`
  - Each has: server `*HubCommand.cs` feature file, `GameHub` method with `TryGetCharacterId` guard + try/catch, client `ReactiveCommand`, `OnXxx` callback, and a `CharacterSelectViewModel` hub subscription.
- **Hub session methods (no mediator by design):** `SelectCharacter`, `EnterZone`, `LeaveZone`, `GetActiveCharacters`, `OnConnectedAsync`, `OnDisconnectedAsync`
- **P3 stubs fixed:** `MainMenuViewModel.SettingsCommand`, `SettingsViewModel.ServerUrl`, all `IXxxRepository` DI registrations
- **Client HTTP services fully tested:** `HttpAuthService` (all methods), `HttpCharacterService`, `HttpZoneService`, `HttpContentService` (11 content types × list + single = 22 methods)
- **`CharacterSelected` payload extended** to include blob stats; `GameViewModel.SeedInitialStats` eliminates all-zero HUD on login.

---

## Goals — in priority order

### 1. Extend `IContentService` / `HttpContentService` for the 6 new content types

Six new content endpoints exist on the server (added in the data-contracts session) and their DTOs exist in `RealmUnbound.Contracts/Content/ContentContracts.cs`, but `RealmUnbound.Client/Services/ContentService.cs` has no methods for them yet.

Add to **`IContentService`** (interface) and **`HttpContentService`** (implementation) — follow the identical pattern used by the 11 existing types (one-liner `GetListAsync<T>` and `GetSingleAsync<T>` delegations):

| Interface methods to add | Route (list) | Route (single) |
|---|---|---|
| `GetOrganizationsAsync()` / `GetOrganizationAsync(string slug)` | `api/content/organizations` | `api/content/organizations/{slug}` |
| `GetWorldLocationsAsync()` / `GetWorldLocationAsync(string slug)` | `api/content/world-locations` | `api/content/world-locations/{slug}` |
| `GetDialoguesAsync()` / `GetDialogueAsync(string slug)` | `api/content/dialogues` | `api/content/dialogues/{slug}` |
| `GetActorInstancesAsync()` / `GetActorInstanceAsync(string slug)` | `api/content/actor-instances` | `api/content/actor-instances/{slug}` |
| `GetMaterialPropertiesAsync()` / `GetMaterialPropertyAsync(string slug)` | `api/content/material-properties` | `api/content/material-properties/{slug}` |
| `GetTraitDefinitionsAsync()` / `GetTraitDefinitionAsync(string key)` | `api/content/traits` | `api/content/traits/{key}` *(note: key, not slug)* |

Return types (all defined in `RealmUnbound.Contracts.Content`):
`OrganizationDto`, `WorldLocationDto`, `DialogueDto`, `ActorInstanceDto`, `MaterialPropertyDto`, `TraitDefinitionDto`

### 2. Tests for the 6 new content service methods

Add tests to `RealmUnbound.Client.Tests/HttpContentServiceTests.cs`. Use the **`FakeHttpHandler` pattern** already established for the existing 11 types. Each new type needs:
- `GetXxxAsync_ReturnsItems_OnSuccess` — 200 response with JSON array
- `GetXxxAsync_ReturnsEmpty_OnError` — non-2xx response → empty list (no throw)
- `GetXxxBySlugAsync_ReturnsItem_OnSuccess` — 200 single-item response
- `GetXxxBySlugAsync_ReturnsNull_OnNotFound` — 404 → null (no throw)

(`TraitDefinition` uses `key` instead of `slug` in the test method name, but the pattern is identical.)

### 3. New hub bridge — `CraftItem`

This is the next highest-value unimplemented gameplay feature. Implement end-to-end:

**Server** — new file `RealmUnbound.Server/Features/Characters/CraftItemHubCommand.cs`:
- `record CraftItemHubCommand(Guid CharacterId, string RecipeSlug) : IRequest<CraftItemHubResult>`
- `record CraftItemHubResult(bool Success, string? ErrorMessage, string? ItemCrafted)`
- Handler reads the character's blob, validates the recipe slug is non-empty, deducts ingredient costs from Gold (`DefaultCraftingCost = 50`), returns success with the recipe slug as `ItemCrafted`
- Blob key: `Gold` (same key already used by `RestAtLocationHubCommandHandler.KeyGold`)
- Over-spend guard: if `currentGold < DefaultCraftingCost`, fail with `"Not enough gold to craft this item"`
- Broadcasts `ItemCrafted` payload: `{ CharacterId, RecipeSlug, GoldSpent, RemainingGold }`

**`GameHub.cs`** — add method `CraftItem(string recipeSlug)`:
- `TryGetCharacterId` guard
- `mediator.Send(new CraftItemHubCommand(characterId, recipeSlug))`
- On success: `Clients.Group(zoneId).SendAsync("ItemCrafted", payload))` or caller fallback
- On failure/exception: `Clients.Caller.SendAsync("Error", message)`

**Client** — `GameViewModel.cs`:
- `CraftItemCommand : ReactiveCommand<string, Unit>` → `DoCraftItemAsync(string recipeSlug)`
- `OnItemCrafted(string recipeSlug, int goldSpent, int remainingGold)` — updates `Gold` property + `AppendLog`

**Client** — `CharacterSelectViewModel.cs`:
- `ItemCraftedPayload` internal record
- `_itemCraftedSub` field + disposal + `"ItemCrafted"` subscription in `DoSelectAsync`

**Tests**: Add to `GameHubTests.cs`: no-character guard, dispatches command, broadcasts to zone group, caller fallback when no zone, error on insufficient gold, mediator-throws error. Add handler-level tests (same `*HubCommandHandler` pattern as existing). Add `GameViewModel` tests for `DoCraftItemAsync` and `OnItemCrafted`.

### 4. New hub bridge — `EnterDungeon`

**Known gotcha (critical):** `EnterDungeonHandler` in Core uses `private static Dictionary<string, DungeonInstance> _activeDungeons`. Server tests that call `EnterDungeon` must clear this static state between tests using the `ActiveDungeonScope` reflection helper pattern established in `engine-codebase.md`.

**Server** — new file `RealmUnbound.Server/Features/Characters/EnterDungeonHubCommand.cs`:
- `record EnterDungeonHubCommand(Guid CharacterId, string DungeonSlug) : IRequest<EnterDungeonHubResult>`
- `record EnterDungeonHubResult(bool Success, string? ErrorMessage, string? DungeonId)`
- Handler uses `IDungeonRepository` (or equivalent) to look up the dungeon by slug; on success returns a `DungeonId`
- **Check Program.cs**: verify `IDungeonRepository` (or whatever abstraction Core uses) is registered. Add `AddScoped` if missing.

**`GameHub.cs`** — `EnterDungeon(string dungeonSlug)`:
- Standard pattern: `TryGetCharacterId` → `mediator.Send` → broadcast `DungeonEntered` to zone group or caller fallback → catch + `Error`

**Client** — follow the same command/callback/subscription pattern as other bridges.

---

## Process

- Use the Explore subagent to run a gap analysis before writing any code. Cross-check `.github/copilot-memory/unbound-memory.md` so already-complete items are not re-attempted.
- Fix stubs before writing tests — tests against broken stubs must be rewritten.
- Do Goals 1 + 2 first (content service extension + tests). It is a clean, low-risk batch that increases coverage and unlocks content-aware ViewModels. Then tackle Goals 3 + 4.
- Run `dotnet build RealmUnbound.slnx` and `dotnet test RealmUnbound.slnx --filter Category!=UI` after each goal batch.
- Every new `GameHub` dispatch method must: validate character ownership first via `TryGetCharacterId`; wrap the mediator call in try/catch and send an `"Error"` message back to the caller on failure; broadcast the result to the zone group with `Clients.Group(zoneId)`, not just the caller.
- Every new or updated `HttpXxxService` method must be covered by a `FakeHttpHandler`-based test for both success and non-2xx paths.
- After any change to `Program.cs` service registrations, run `dotnet build RealmUnbound.Server` immediately and verify it compiles clean. The server uses `AddRealmEngineCore(p => p.UseExternal())` which skips persistence-layer registrations — every `IXxxRepository` and `IXxxService` required by a Core handler must be explicitly `AddScoped` in Program.cs.

## Wrap-up

- Write any non-obvious constraints, gotchas, or architectural decisions discovered during the session directly into `.github/copilot-memory/unbound-memory.md` (session log section + relevant technical section). Only record things that would have caused wasted time if unknown at the start of a future session.
- Update test counts in `.github/copilot-memory/unbound-memory.md` (Session Log table).

## Rules that must never be broken

- Never suppress CS1591. Never add `NoWarn` entries to any `.csproj`.
- Never create breadcrumb or placeholder files — finish the work or don't create the file.
- Never apply `[Obsolete]` — always move forward with new implementations.
- Never call `mediator.Send(...)` from a hub method without first verifying the character belongs to the caller via `IActiveCharacterTracker`.
- Never bypass SignalR authentication — hub methods that modify game state require an authenticated connection (JWT from query string is already wired in Program.cs).
- Engine libraries (Core, Shared, Data) must remain UI-agnostic — no Avalonia or SignalR references in those projects.
- Do NOT store string-valued data in the `Attributes` blob — it is deserialized as `Dictionary<string, int>`; mixing types causes `JsonException`. All blob keys are `int`. Use `EquipmentBlob` (a separate `Dictionary<string, string>` column) for string-valued slot data.
