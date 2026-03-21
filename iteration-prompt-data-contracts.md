Wire up all remaining content catalog operations end-to-end: Core MediatR query handler → registered in `Program.cs` → unit-tested.

The HTTP endpoints and repositories are all done. The gap is that 11 content types have a live `GET /api/content/{type}` route and a registered `IXxxRepository` but no MediatR handler in `RealmEngine.Core` that a consumer can call via `mediator.Send(...)`. This session fills that gap completely.

Setup:
- Read `.github/copilot-memory/engine-codebase.md` and `.github/copilot-memory/json-migration-status.md` before writing any code. Cross-check those files AND the "Completed in previous sessions" section below so already-completed work is not re-attempted.
- Run `dotnet test RealmEngine.slnx` to establish the baseline. All work must finish with zero regressions against that baseline.
- Baseline (session-19): Shared=778, Core=1,738, Data=203, Server=362.

Completed in previous sessions (do not re-implement):
- All EF Core repositories implemented, registered in `Program.cs`, and tested: `ISpeciesRepository`, `IItemRepository`, `IEnchantmentRepository`, `INodeRepository`, `IAbilityRepository`, `IArmorRepository`, `IWeaponRepository`, `IMaterialRepository`, `ISkillRepository`, `ISpellRepository`, `IQuestRepository`, `IRecipeRepository`, `ILootTableRepository`, `INpcRepository`, `IEnemyRepository`, `IEquipmentSetRepository`, `ICharacterClassRepository`, `IBackgroundRepository`, `INamePatternRepository`, `IHallOfFameRepository`
- `DatabaseSeeder`: `SeedMaterialsAsync`, `SeedMaterialPropertiesAsync`, `SeedActorClassesAsync`, `SeedAbilitiesAsync`, `SeedSkillsAsync`, `SeedBackgroundsAsync`, `SeedSpeciesAsync`, `SeedItemsAsync` (17 items), `SeedEnchantmentsAsync` (9 enchantments), `SeedContentRegistryAsync` — all done
- All `GET /api/content/{type}` endpoints wired — abilities, enemies, npcs, quests, recipes, loot-tables, spells, classes, species, backgrounds, skills, items, enchantments, weapons, armors, materials (+ slug variants for all)
- Contract DTOs in `RealmUnbound.Contracts`: `AbilityDto`, `EnemyDto`, `NpcDto`, `QuestDto`, `RecipeDto`/`RecipeMaterialDto`, `LootTableDto`/`LootTableEntryDto`, `SpellDto`, `ActorClassDto`, `SpeciesDto`, `BackgroundDto`, `SkillDto`, `ItemDto`, `EnchantmentDto`, `WeaponDto`, `ArmorDto`, `MaterialDto`
- Core catalog query handlers (with FluentValidation validators) and unit tests: `GetSpeciesQuery`, `GetItemCatalogQuery`, `GetEnchantmentCatalogQuery`
- Gameplay queries/commands in Core (not catalog): `GetBackgroundsQuery`/`GetBackgroundQuery`, `GetCharacterClassesQuery`/`GetAvailableClassesQuery`/`GetClassDetailsQuery` etc.
- Integration tests: `ContentTypedEndpointTests.cs` (classes, species, backgrounds, skills, enchantments, abilities, items), `ContentExtendedEndpointTests.cs` (enemies, npcs, quests, recipes, loot-tables, spells), `ContentEquipmentEndpointTests.cs` (weapons, armors, materials), `ContentBrowseEndpointTests.cs` (browse + schema)

---

## Goals — in priority order

### 1. Core catalog query handlers for the 11 missing content types

Each handler must follow the established pattern used by `GetSpeciesQuery`, `GetItemCatalogQuery`, and `GetEnchantmentCatalogQuery`:
- One file per content type under `RealmEngine.Core/Features/{Domain}Catalog/Queries/Get{Domain}CatalogQuery.cs`
- Three things per file: `record Get{Domain}CatalogQuery(...) : IRequest<IReadOnlyList<TModel>>`, `class Get{Domain}CatalogQueryHandler`, `class Get{Domain}CatalogQueryValidator`
- Full XML doc comments on all three (CS1591 is a hard compile error)
- Validator: when the optional filter param is non-null, apply `NotEmpty` + `MaximumLength(100)` rules

The 11 handlers to add, with their optional filter parameter and the repository method it maps to:

| Feature folder | Query record | Filter param | Repository method when filter is set |
|---|---|---|---|
| `AbilityCatalog` | `GetAbilityCatalogQuery(string? AbilityType = null)` | `AbilityType` | `IAbilityRepository.GetByTypeAsync(abilityType)` |
| `EnemyCatalog` | `GetEnemyCatalogQuery(string? Family = null)` | `Family` | `IEnemyRepository.GetByFamilyAsync(family)` |
| `NpcCatalog` | `GetNpcCatalogQuery(string? Category = null)` | `Category` | `INpcRepository.GetByCategoryAsync(category)` |
| `QuestCatalog` | `GetQuestCatalogQuery(string? TypeKey = null)` | `TypeKey` | `IQuestRepository.GetByTypeKeyAsync(typeKey)` |
| `RecipeCatalog` | `GetRecipeCatalogQuery(string? CraftingSkill = null)` | `CraftingSkill` | `IRecipeRepository.GetByCraftingSkillAsync(craftingSkill)` |
| `LootTableCatalog` | `GetLootTableCatalogQuery(string? Context = null)` | `Context` | `ILootTableRepository.GetByContextAsync(context)` |
| `SpellCatalog` | `GetSpellCatalogQuery(string? School = null)` | `School` | `ISpellRepository.GetBySchoolAsync(school)` |
| `SkillCatalog` | `GetSkillCatalogQuery(string? Category = null)` | `Category` | `ISkillRepository.GetByCategoryAsync(category)` |
| `WeaponCatalog` | `GetWeaponCatalogQuery()` | *(none — no filter method on `IWeaponRepository`)* | `IWeaponRepository.GetAllAsync()` |
| `ArmorCatalog` | `GetArmorCatalogQuery()` | *(none — no filter method on `IArmorRepository`)* | `IArmorRepository.GetAllAsync()` |
| `MaterialCatalog` | `GetMaterialCatalogQuery(string? Family = null)` | `Family` | `IMaterialRepository.GetByFamiliesAsync([family])` (wrap single value in a list) |

Return types:
- `GetAbilityCatalogQuery` → `IReadOnlyList<Ability>` (shared model)
- `GetEnemyCatalogQuery` → `IReadOnlyList<Enemy>`
- `GetNpcCatalogQuery` → `IReadOnlyList<NPC>`
- `GetQuestCatalogQuery` → `IReadOnlyList<Quest>`
- `GetRecipeCatalogQuery` → `IReadOnlyList<Recipe>`
- `GetLootTableCatalogQuery` → `IReadOnlyList<LootTableData>`
- `GetSpellCatalogQuery` → `IReadOnlyList<Spell>`
- `GetSkillCatalogQuery` → `IReadOnlyList<SkillDefinition>`
- `GetWeaponCatalogQuery` → `IReadOnlyList<Item>` (`IWeaponRepository` returns `Item` projections)
- `GetArmorCatalogQuery` → `IReadOnlyList<Item>` (`IArmorRepository` returns `Item` projections)
- `GetMaterialCatalogQuery` → `IReadOnlyList<MaterialEntry>`

### 2. Unit tests for each new handler

Each handler needs tests in `RealmEngine.Core.Tests/Features/{Domain}Catalog/` (create the folder). Follow the pattern established by `GetSpeciesQueryTests`, `GetItemCatalogQueryTests`, `GetEnchantmentCatalogQueryTests` in `Core.Tests`. Tests per handler:
- `Handle_NoFilter_ReturnsAllFromRepository`
- `Handle_WithFilter_CallsFilteredMethod` (where a filter exists)
- `Handle_WithFilter_PassesFilterValueToRepository` (where a filter exists)
- Validator tests: `Validator_NullFilter_IsValid`, `Validator_ValidFilter_IsValid`, `Validator_EmptyStringFilter_IsInvalid`, `Validator_TooLongFilter_IsInvalid` (where a filter exists)
- `WeaponCatalog` and `ArmorCatalog` only need: `Handle_ReturnsAllFromRepository` + `Validator_AlwaysValid`

### 3. The six unrepresented ContentDbContext tables

These six `DbSet<T>` tables exist in `ContentDbContext` and have EF entity classes but no repository interface, no shared model, no endpoint, no contract DTO, and no Core handler. Implement all six end-to-end using the same layered pattern as Goal 1. Work through them one at a time in the order listed below. For each table:

1. Add a shared model record to `RealmEngine.Shared/Models/`
2. Add `IXxxRepository` to `RealmEngine.Shared/Abstractions/` with full XML docs
3. Add `EfCoreXxxRepository` to `RealmEngine.Data/Repositories/` with `/// <inheritdoc />`
4. Register in `RealmUnbound.Server/Program.cs` (`AddScoped<IXxx, EfCoreXxx>()`)
5. Add contract DTO to `RealmUnbound.Contracts/Content/ContentContracts.cs` with full XML `<summary>` + `<param>` docs
6. Add `GET /api/content/{route}` and `GET /api/content/{route}/{identifier}` to `ContentEndpoints.cs`
7. Add `Get{Domain}CatalogQuery` handler + validator to `RealmEngine.Core/Features/{Domain}Catalog/Queries/`
8. Add unit tests to `RealmEngine.Core.Tests/Features/{Domain}Catalog/` and `RealmEngine.Data.Tests/Repositories/`
9. Run `dotnet build RealmEngine.slnx` + `dotnet build RealmUnbound.Server` after steps 3–4, then `dotnet test RealmEngine.slnx` after adding tests

Run `dotnet build RealmUnbound.Server` after **every** `Program.cs` change.

---

#### Table A — `Organizations`

**Entity:** `Organization : ContentBase` — extra field: `OrgType` (string)

**Shared model** (`RealmEngine.Shared/Models/OrganizationEntry.cs`):
```csharp
public record OrganizationEntry(
    string Slug, string DisplayName, string TypeKey, string OrgType, int RarityWeight);
```

**Interface** (`IOrganizationRepository`) — three methods:
- `Task<List<OrganizationEntry>> GetAllAsync()`
- `Task<OrganizationEntry?> GetBySlugAsync(string slug)`
- `Task<List<OrganizationEntry>> GetByTypeAsync(string orgType)` — filters by `OrgType` (lowercased)

**EfCore repo** — uses `ContentDbContext.Organizations`, `Where(o => o.IsActive)`, maps `Slug/DisplayName/TypeKey/OrgType/RarityWeight`

**Contract DTO:**
```csharp
public record OrganizationDto(string Slug, string DisplayName, string TypeKey, string OrgType, int RarityWeight);
```

**Routes:** `GET /api/content/organizations` (optional `?orgType=`), `GET /api/content/organizations/{slug}`

**Core query:** `GetOrganizationCatalogQuery(string? OrgType = null)` → `IReadOnlyList<OrganizationEntry>` — filter calls `GetByTypeAsync(orgType)`, no filter calls `GetAllAsync()`

---

#### Table B — `WorldLocations`

**Entity:** `WorldLocation : ContentBase` — extra fields: `LocationType` (string), `Stats.MinLevel`, `Stats.MaxLevel`

**Shared model** (`RealmEngine.Shared/Models/WorldLocationEntry.cs`):
```csharp
public record WorldLocationEntry(
    string Slug, string DisplayName, string TypeKey, string LocationType,
    int RarityWeight, int? MinLevel, int? MaxLevel);
```

**Interface** (`IWorldLocationRepository`):
- `Task<List<WorldLocationEntry>> GetAllAsync()`
- `Task<WorldLocationEntry?> GetBySlugAsync(string slug)`
- `Task<List<WorldLocationEntry>> GetByLocationTypeAsync(string locationType)` — filters by `LocationType` (lowercased)

**Contract DTO:**
```csharp
public record WorldLocationDto(string Slug, string DisplayName, string TypeKey,
    string LocationType, int RarityWeight, int? MinLevel, int? MaxLevel);
```

**Routes:** `GET /api/content/world-locations` (optional `?locationType=`), `GET /api/content/world-locations/{slug}`

**Core query:** `GetWorldLocationCatalogQuery(string? LocationType = null)` → `IReadOnlyList<WorldLocationEntry>`

---

#### Table C — `Dialogues`

**Entity:** `Dialogue : ContentBase` — extra fields: `Speaker` (string?), `Stats.Lines` (List\<string\>)

**Shared model** (`RealmEngine.Shared/Models/DialogueEntry.cs`):
```csharp
public record DialogueEntry(
    string Slug, string DisplayName, string TypeKey, string? Speaker,
    int RarityWeight, List<string> Lines);
```

**Interface** (`IDialogueRepository`):
- `Task<List<DialogueEntry>> GetAllAsync()`
- `Task<DialogueEntry?> GetBySlugAsync(string slug)`
- `Task<List<DialogueEntry>> GetBySpeakerAsync(string speaker)` — filters by `Speaker` (lowercased), null Speaker rows are excluded

**Contract DTO:**
```csharp
public record DialogueDto(string Slug, string DisplayName, string TypeKey,
    string? Speaker, int RarityWeight, List<string> Lines);
```

**Routes:** `GET /api/content/dialogues` (optional `?speaker=`), `GET /api/content/dialogues/{slug}`

**Core query:** `GetDialogueCatalogQuery(string? Speaker = null)` → `IReadOnlyList<DialogueEntry>`

---

#### Table D — `ActorInstances`

**Entity:** `ActorInstance : ContentBase` — extra fields: `ArchetypeId` (Guid), `LevelOverride` (int?), `FactionOverride` (string?)

**Shared model** (`RealmEngine.Shared/Models/ActorInstanceEntry.cs`):
```csharp
public record ActorInstanceEntry(
    string Slug, string DisplayName, string TypeKey,
    Guid ArchetypeId, int? LevelOverride, string? FactionOverride, int RarityWeight);
```

**Interface** (`IActorInstanceRepository`):
- `Task<List<ActorInstanceEntry>> GetAllAsync()`
- `Task<ActorInstanceEntry?> GetBySlugAsync(string slug)`
- `Task<List<ActorInstanceEntry>> GetByTypeKeyAsync(string typeKey)` — filters by `TypeKey`

**EfCore repo** — do NOT include navigation property in query (no `.Include`); map only the flat fields listed above.

**Contract DTO:**
```csharp
public record ActorInstanceDto(string Slug, string DisplayName, string TypeKey,
    Guid ArchetypeId, int? LevelOverride, string? FactionOverride, int RarityWeight);
```

**Routes:** `GET /api/content/actor-instances` (optional `?typeKey=`), `GET /api/content/actor-instances/{slug}`

**Core query:** `GetActorInstanceCatalogQuery(string? TypeKey = null)` → `IReadOnlyList<ActorInstanceEntry>`

---

#### Table E — `MaterialProperties`

**Entity:** `MaterialProperty : ContentBase` — extra fields: `MaterialFamily` (string), `CostScale` (float)
*(Distinct from `Material` — this is the property-definition vocabulary table, not the list of named materials.)*

**Shared model** (`RealmEngine.Shared/Models/MaterialPropertyEntry.cs`):
```csharp
public record MaterialPropertyEntry(
    string Slug, string DisplayName, string TypeKey,
    string MaterialFamily, float CostScale, int RarityWeight);
```

**Interface** (`IMaterialPropertyRepository`):
- `Task<List<MaterialPropertyEntry>> GetAllAsync()`
- `Task<MaterialPropertyEntry?> GetBySlugAsync(string slug)`
- `Task<List<MaterialPropertyEntry>> GetByFamilyAsync(string family)` — filters by `MaterialFamily` (lowercased)

**Contract DTO:**
```csharp
public record MaterialPropertyDto(string Slug, string DisplayName, string TypeKey,
    string MaterialFamily, float CostScale, int RarityWeight);
```

**Routes:** `GET /api/content/material-properties` (optional `?family=`), `GET /api/content/material-properties/{slug}`

**Core query:** `GetMaterialPropertyCatalogQuery(string? Family = null)` → `IReadOnlyList<MaterialPropertyEntry>`

---

#### Table F — `TraitDefinitions`

**Entity:** `TraitDefinition` — **NOT a `ContentBase`**. Uses `Key` (string) as the natural identifier. No `Slug`, no `IsActive`, no `RarityWeight`. Lives in `RealmEngine.Data/Entities/Support/TraitDefinition.cs`.

**Shared model** (`RealmEngine.Shared/Models/TraitDefinitionEntry.cs`):
```csharp
public record TraitDefinitionEntry(
    string Key, string ValueType, string? Description, string? AppliesTo);
```

**Interface** (`ITraitDefinitionRepository`) — note: no `GetBySlugAsync`, no `IsActive` filter:
- `Task<List<TraitDefinitionEntry>> GetAllAsync()`
- `Task<TraitDefinitionEntry?> GetByKeyAsync(string key)`
- `Task<List<TraitDefinitionEntry>> GetByAppliesToAsync(string entityType)` — returns rows where `AppliesTo == "*"` OR `AppliesTo` contains `entityType` (case-insensitive substring match)

**EfCore repo** — uses `ContentDbContext.TraitDefinitions` (`DbSet<TraitDefinition>`). No `IsActive` filter (entity has no such property). For `GetByAppliesToAsync`, use: `Where(t => t.AppliesTo == "*" || (t.AppliesTo != null && t.AppliesTo.Contains(entityType)))`.

**Contract DTO:**
```csharp
public record TraitDefinitionDto(string Key, string ValueType, string? Description, string? AppliesTo);
```

**Routes:** `GET /api/content/traits` (optional `?appliesTo=`), `GET /api/content/traits/{key}` *(uses `key` not `slug` in the route — the handler injections use `ITraitDefinitionRepository.GetByKeyAsync(key)`, not `GetBySlugAsync`)*

**Core query:** `GetTraitCatalogQuery(string? AppliesTo = null)` → `IReadOnlyList<TraitDefinitionEntry>` — filter calls `GetByAppliesToAsync(appliesTo)`, no filter calls `GetAllAsync()`

**Validator special case:** the validator for `GetTraitCatalogQuery` follows the same pattern (NotEmpty + MaximumLength(100) when non-null), but the filter param name is `AppliesTo`.

**Tests special cases for TraitDefinition:**
- `EfCoreTraitDefinitionRepository` tests do NOT call `.Where(x => x.IsActive)` — seed rows directly without `IsActive`, and verify they all come back
- `GetByAppliesToAsync("enemies")` must return rows with `AppliesTo = "*"` AND rows with `AppliesTo = "enemies,weapons"` — seed both to cover the OR logic

---

## Process

- Use the Explore subagent to run a gap analysis before writing any code. Cross-check `.github/copilot-memory/` so already-completed items are not re-reported.
- Implement handlers in batches of 3–4; run `dotnet build RealmEngine.slnx` after each batch to catch CS1591 before it accumulates.
- After all 11 handlers and their tests are in, run `dotnet test RealmEngine.slnx` to confirm zero regressions. The Core.Tests count should increase by roughly 8–10 tests per handler with a filter, 2–3 for handlers without.
- `CharacterGrowthService.GetClassMultipliers` accepts a `@classes/{typeKey}:{slug}` string — this is intentional config-schema design, not a migration gap. Do not change it.
- New migrations go in `RealmEngine.Data/Migrations/` (ContentDbContext) or `RealmEngine.Data/Migrations/GameDb/` (GameDbContext). Never add a migration to `RealmUnbound.Server/Migrations/` unless it targets `ApplicationDbContext`.

## Wrap-up

- If any non-obvious constraints, gotchas, or architectural decisions were discovered during the session, write them into `.github/copilot-memory/engine-codebase.md` or `.github/copilot-memory/json-migration-status.md` (edit directly using file tools). Only record things that would have caused wasted time if unknown at the start of a future session.
- Update `.github/copilot-memory/json-migration-status.md` with what was completed and what still remains.

## Rules that must never be broken

- Never suppress CS1591. Never add `NoWarn` entries to any `.csproj`.
- Never create breadcrumb or placeholder files — finish the work or don't create the file.
- Never apply `[Obsolete]` — always move forward with new implementations.
- Engine libraries (`Core`, `Shared`, `Data`) must remain UI-agnostic — no Avalonia, SignalR, or ASP.NET Core references in those projects.
- Never read content from the filesystem in `RealmEngine.Data` or `RealmEngine.Core`. All content comes from the database. If a test needs content data, seed it in the test setup.
- `Character.ClassName` (the server EF entity) stores the class `DisplayName` (e.g. `"Warrior"`). Never store a slug or `@classes/...` format in that field.
- `@classes/...` strings belong only inside the `growth-stats` `GameConfig` JSON blob. Do not introduce this format anywhere else.
