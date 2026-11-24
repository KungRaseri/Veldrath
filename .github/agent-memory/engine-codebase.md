# RealmEngine Codebase Notes

> **Status**: Updated through Session-69 (2026-10-25).

## Test Counts

Five test projects remain; the desktop client, `Veldrath.GameClient.Core`, and `Veldrath.Auth`
were deleted with its retirement, and `Veldrath.Assets` / `Veldrath.Assets.Tests` went on
2026-11-04.

| Project | Tests |
|---------|-------|
| `RealmEngine.Core.Tests` | 2,040 |
| `RealmEngine.Shared.Tests` | 795 |
| `RealmEngine.Data.Tests` | 301 |
| `Veldrath.Server.Tests` | 846 |
| `Veldrath.Discord.Tests` | 29 |
| **Total (verified 2026-11-04 with `dotnet test Realm.Full.slnx --no-build`, after `Veldrath.Assets` was deleted)** | **4,011** |

The count is per-project `Passed:` totals from that command, not a hand tally — re-run it instead of
adjusting the table by hand.

### Coverage must be measured in `Release`, not `Debug`

`dotnet test` in the default `Debug` configuration reports **five to six thousand more coverable
lines** than CI does and lands about **1.5 points lower on the global total** — measured 2026-11-04:
Debug 27,827/37,632 = 73.95%, Release 24,155/31,995 = 75.50% on the same tree. The floors live in
[`scripts/coverage-floors.json`](../../scripts/coverage-floors.json:1) and were measured in Release
(`ci-coverage.yml` sets `CONFIGURATION: Release`), so a Debug run makes a green tree look red:
`RealmEngine.Shared` reads 54.98% in Debug against its floor of 55 and 56.38% in Release. Reproduce
the gate exactly as the workflow does:

```bash
dotnet build Realm.Full.slnx --configuration Release
dotnet test Realm.Full.slnx --configuration Release --no-build \
  --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory ./TestResults
bash scripts/coverage-report.sh "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" "-reporttypes:Html;Cobertura;MarkdownSummaryGithub;TextSummary"
node scripts/check-dotnet-coverage.mjs --report ./CoverageReport/Cobertura.xml \
  --floors scripts/coverage-floors.json --suggest
```

The reporter is [`scripts/coverage-report.sh`](../../scripts/coverage-report.sh:1) — it clones the
`codecov/codecov-report` tool into `.coverage-report/<ref>` on first use and needs no global tool or
licence (PR #46; it replaced `dotnet-reportgenerator-globaltool`). Its class and method counts are
**not** ReportGenerator's: on the 2026-11-04 report it printed 684 classes / 2,058 methods where
ReportGenerator printed 677 / 2,711, so method coverage (79.4%) and full-method coverage (68.0%) are
only comparable between runs of the same tool. Line and branch are identical either way — 75.5% /
55.8% from both — which is what the floors gate on. The `-reporttypes` list above is the workflow's
own, so the local report contains the same files CI uploads.

`RealmForge.Tests` (27 tests) was deleted with the rest of the Avalonia tooling in Milestone B.
`Veldrath.Server.Tests` lost ~107 tests with the deleted `/api/auth/*` endpoint suites; the
integration tests now authenticate through `WebAppFactory.CreateAccountTokenAsync`.

The SvelteKit app adds Vitest + Playwright suites under `web/`.

## Key Model Facts
- `Location` has 4 required properties: `Id`, `Name`, `Description`, `Type`
- `Background` uses `Slug` (not `Id`) as its identifier
- `Enemy.Prefixes` and `Enemy.Suffixes` are `List<NameComponent>`, not `List<string>`
- `SaveGame` uses `PlayerName` (not `CharacterName`)
- `NameComponent` has `Token` (semantic key) and `Value` (display text) properties
- `Character.GainExperience()` auto-levels at `Level * 100` XP
- `InventoryRecord` now has nullable `Durability` column (added migration `AddInventoryRecordDurability`)

### Assigning a child collection is an UPDATE, not an insert (A0, 2026-11-20)

EF's change detection treats a deserialised child whose generated key is already set
(`LootTableEntry.Id = Guid.NewGuid()`) as an existing row, so assigning the authored list onto a tracked parent
issues an UPDATE for a row that was never inserted — `DbUpdateConcurrencyException`, nothing written. Any update
path that owns child rows (the content-pack importer does, `RealmEngine.Data/Authoring/ContentPacks/
ContentPackImporter.ReconcileChildren`) has to reconcile them: match on authored identity, mutate the match,
`db.RemoveRange` what the pack dropped, and `db.Add` what it added with the parent FK set explicitly.

## Positional Records (must use positional ctor, not object initializer)
- `RestAtInnCommand(string LocationId, string CharacterName, int Cost = 10)`
- `VisitShopCommand(string LocationId, string CharacterName)`
- `RefreshMerchantInventoryCommand(string MerchantId)`
- `GetBackgroundQuery(string BackgroundId)`
- `GetBackgroundsQuery(string? FilterByAttribute = null)`

## IInventoryService Interface
Methods: `AddItemsAsync`, `AddItemAsync`, `HasInventorySpaceAsync`, `GetItemCountAsync`, `RemoveItemAsync`, `ReduceItemDurabilityAsync`
- `InMemoryInventoryService`: in-memory dict; tracks durabilities in `_durabilities` dict (default 100)
- `EfCoreInventoryService`: uses `GameDbContext.InventoryRecords` table

## Handler Behaviors
- `EncounterBossCommandHandler.GenerateBossTitle`: uses `Prefixes.Select(p => p.Value)` for title (bug was fixed — was using `string.Join(" ", boss.Prefixes)` which called ToString())
- `ExploreLocationCommandHandler`: injects `IRandomSource` (optional ctor parameter) for the 60% combat / 40% peaceful roll; tests script a `FakeRandomSource`
- `HarvestNodeCommandHandler`: calls `_inventoryService.ReduceItemDurabilityAsync` after calculating `durabilityLoss`
- `PartyCombatTurnHandler`: `CombatService.ExecuteEnemyAttack` already applies its `finalDamage` to
  `Character.Health`, so the handler must only read `enemyResult.Damage` for the result and the log. Both
  enemy branches (party → leader, and no-party) used to subtract it a second time and dealt double damage;
  fixed 2026-11-05 and pinned by exact-HP assertions in `PartyCombatTurnHandlerPartyTests`.

### Randomness — `IRandomSource` (2026-11-03)

- `IRandomSource` (`RealmEngine.Shared/Abstractions/IRandomSource.cs`) exposes `Next(int)`,
  `Next(int, int)` and `NextDouble()`. `SystemRandomSource`
  (`RealmEngine.Shared/Services/SystemRandomSource.cs`) wraps `Random.Shared` and exposes a
  process-wide `Shared` instance.
- `AddRealmEngineCore` registers `IRandomSource` as a singleton, so every DI-constructed handler and
  service (including `Veldrath.Server` hub command handlers and `GameHub`) resolves the same binding.
- Every former direct `Random.Shared` call site in combat/exploration/progression now takes an
  optional `IRandomSource? randomSource = null` constructor parameter that defaults to
  `SystemRandomSource.Shared`. Optional parameters keep existing direct-construction tests compiling
  and are honoured by `Microsoft.Extensions.DependencyInjection`; a test host can register its own
  binding instead.
- The static utilities keep their old signatures and add overloads: `DiceRoller.Roll*` /
  `DiceRoller.RollDiceString` and `WeightedSelector.SelectByRarityWeight` accept an
  `IRandomSource`. `CombatHelpers.RollEnemyAbility` (internal) now *requires* one — the four combat
  hub handlers inject it.
- Test fakes live in `RealmEngine.Core.Tests/Infrastructure/FakeRandomSource.cs` and
  `Veldrath.Server.Tests/Infrastructure/FakeRandomSource.cs`. Scripted mode
  (`FakeRandomSource.Returning(0, 99, ...)`) returns values in call order and throws when the script
  runs out, so a forgotten roll fails the test instead of silently going random; `Seeded(n)` wraps
  `Random(n)` when only run-to-run stability matters.
- Do not write "roll until the branch lands" loops in combat tests any more — scripted fakes are the
  convention (`CombatServiceTests`, `ExploreLocationCommandHandlerTests`,
  `FleeFromCombatHubCommandHandlerTests`, `FleeFromCombatHandlerTests`, `DungeonGeneratorServiceTests`,
  `ApplyStatusEffectTests`, `UsePowerHandlerTests`, `CastSpellHandlerTests`).
- `QuestIntegrationTests` (`Features/Quest/Integration/`) drives real `AttackEnemyCommand` loops, so it
  registers `FakeRandomSource.Seeded(20260101)` and passes that instance to its manually constructed
  `CombatService` (`randomSource:`); without it the loops resolved `SystemRandomSource.Shared` and the
  round count — and therefore coverage — moved between runs. Its loops are bounded by
  `MaxAttackRounds` (200) and assert the enemy reached 0 HP, so a combat-maths regression fails instead
  of hanging. Seeding is enough there because no test asserts a named crit/dodge arm; a test that needs
  one should pass a scripted `FakeRandomSource` instead.
- `SpellCastingService.CastSpell` makes exactly one `NextDouble()` draw per attempt, in
  `CheckCastSuccess`: `rate = clamp(0.90 + 0.005 * (rank - MinimumSkillRank), 0.60, 0.99)` and the
  cast succeeds when `roll < rate` (a numeric `BaseEffectValue` never rolls dice). Script a margin
  away from the boundary: the computed floor rate is `0.6000000000000001`, so scripting exactly
  `0.60` succeeds rather than fizzling.
- `UsePowerHandler` (`Features/Progression/Commands/UseAbilityHandler.cs`) injects the source for both
  the `BaseDamage` dice roll and the status-effect chance roll; that was the last production caller of
  `DiceRoller.RollDiceString(string)`, so the parameterless compatibility overload now has no
  production caller (it stays public API).
- `EnemyPowerAIService` (`Features/Combat/Services/EnemyAbilityAIService.cs` — the file keeps its old
  name, the class is `EnemyPowerAIService`) injects `IRandomSource? randomSource = null`;
  `CombatService` passes its own source through, so a scripted `CombatService` fake also controls
  enemy ability selection. Each `DecideAbilityUsage` call rolls `Next(100)` once per candidate in
  descending priority order (70 defensive below 30% health, 50 buff above 80% health, 45 debuff
  while the player is above 70%, 40 offensive above 50% health, 20 fallback), then a 20%
  `Next(100)` fallback roll plus `Next(availableAbilities.Count)` when every priority roll fails.
  Its tests (`EnemyPowerAIServiceTests.cs`) script those draws and assert the chosen ability and
  `FakeRandomSource.IntCallCount` instead of a statistical threshold.
- Known classification overlap in `EnemyPowerAIService`: `IsOffensiveAbility` matches the words
  "attack"/"damage" anywhere in the description, so a buff power whose description mentions them is
  classified offensive and takes the 40 priority before the 50 buff branch. The buff test avoids
  those words deliberately; the ordering itself is unchanged (not part of the de-flake).
- `EnemySpellCastingService` now takes `IRandomSource? randomSource = null` as well, and `CombatService`
  passes its own source through, so enemy spell selection and the damage/heal dice are scripted too
  (the 95% coverage round needed that to assert specific spell outcomes instead of sampling).
- Still nondeterministic, deliberately out of scope for that pass: `NameComposer`, `LootTableService`,
  item generation and `NodeSpawnerService` keep their own `new Random()`.
- Coverage branch counts are **not** fully deterministic. Three consecutive full runs on the merged
  base agreed (24,213/32,121 lines, 5,823/10,490 branches), but the review of PR #35 measured 5,833,
  5,833 and 5,834 covered of 10,492 branches with identical line counts. The single varying branch was
  `SkillProgressionService.AwardSkillXP` line 92
  (`if (!skill.XPSources.ContainsKey(actionSource))`, the "source already recorded" arm): the only
  test that could reach it was the unseeded combat loops in `QuestIntegrationTests`, and only when
  two critical hits landed on one character (`precision` / `critical_hit` XP on the same character)
  — reproduced once in 30 filtered runs of that class. `SkillProgressionServiceTests` now awards the
  same source twice directly, so the arm is covered on every run; three consecutive merged runs
  afterwards agreed exactly on 24,213/32,121 lines and 5,824/10,490 branches. Do not restate this as
  whole-suite determinism: `NameComposer`, `LootTableService`, item generation and
  `NodeSpawnerService` still keep their own `new Random()`. The 95% round moved every RNG-driven test
  onto seeded/scripted `IRandomSource` values: four full runs of PR #53's head spanned at most 0.02
  points per assembly, and three post-review-correction runs repeated that exactly.
- The gate is live as of 2026-11-03 (PR #21). `scripts/coverage-floors.json` records, per assembly, the
  *lowest* observed full-run rate minus one point (headroom is measured, not hoped for), and its
  `measured` blocks keep the baseline that produced the floor. Floors rise only when a change lifts an
  assembly 3+ points above its floor (D5 rule 2; `--suggest` names the stale ones); the 95% round
  lifted every assembly by more than that, so the 2026-11-04 ratchet is global 94/81 with Core 90/79,
  Server 94/82, Data 98/83, Shared 97/94, Contracts 99/— and Discord 88/91. CI merges the five reports
  with `scripts/coverage-report.sh` (the pinned codecov-report tool), not ReportGenerator.

## Testing Gotchas

### Castle.DynamicProxy / Moq Constraints
- Types used as generic type arguments in `Mock<IFoo<T>>` must be `public`. `file` or `internal` test types used as generic args will cause a Castle proxy error at runtime.
- `EnemyGenerator(IEnemyRepository, ILogger<EnemyGenerator>)` has no parameterless constructor — cannot use `Mock.Of<EnemyGenerator>()`. Construct a real instance: `new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance)`.

### MediatR ValidationBehavior
- `ValidationBehavior` runs validators via `Task.WhenAll` but passes the **same** `ValidationContext` to all of them. Failure messages from one validator appear in every other validator's result set. Testing multi-validator failure aggregation: use **one validator with multiple failing rules**, not multiple separate validators.

### Model Required Members
- `Location` has four `required` members: `Id`, `Name`, `Description`, `Type`. All four must be set in object initializers or tests will not compile (CS9035).
- `DungeonRoom` requires `Id` and `Type`.
- `DungeonInstance` requires `Id`, `LocationId`, and `Name`.

### Item Upgrade Levels
- `Item.GetMaxUpgradeLevel()`: Common=5, Uncommon=5, Rare=7, Epic=9, Legendary=10.
- Tests for upgrade levels > 5 must use `ItemRarity.Legendary` (max=10) or the test will hit an empty remaining-levels list.

### Static State in Handlers
- `EnterDungeonHandler._activeDungeons` is `private static Dictionary<string, DungeonInstance>`. Access via reflection for test setup. Use a file-scoped `ActiveDungeonScope` helper that injects and cleans up the entry in Dispose.

### File Deletion in PowerShell
- `Remove-Item` may be blocked by shell policy. Use `[System.IO.File]::Delete("absolute\path")` as a reliable alternative.

### Enum Confusion
- `RarityTier` enum is in `RealmEngine.Shared.Models`. Separate from `ItemRarity` (used on items). Don't confuse the two.

### Enemy / NPC Dual-Archetype Behavior
- `EfCoreEnemyRepository` and `EfCoreNpcRepository` **both** query `db.ActorArchetypes` without any hostility/role filter. A single seeded `ActorArchetype` row therefore appears in both `/api/content/enemies` and `/api/content/npcs` responses. This is by design — hostility is a trait flag, not a filter applied by the repos.

### Archetype → Enemy Stat Projection (E1, 2026-11-20)
- `EfCoreEnemyRepository.MapToModel` now assigns every stat the archetype authors: `Stats.Health` → `Health` **and** `MaxHealth` (a spawn arrives at full health), `Stats.Damage` → `BasePhysicalDamage`, `Stats.ExperienceReward` → `XP`, `Stats.Mana` → `Mana` + `MaxMana`. A stat the archetype does not author keeps the `Enemy` model default — the projection never hardcodes a second copy of it.
- The four authored attributes are **canonical on the typed `Enemy` properties**: `Stats.Strength`/`Agility`/`Intelligence`/`Constitution` → `Enemy.Strength`/`Dexterity`/`Intelligence`/`Constitution` (`Agility` is the archetype's name for Dexterity). `Enemy.Attributes` is a **derived mirror** filled from those typed values under the existing lowercase keys — never the reverse. `Wisdom`/`Charisma` stay at the model default 10 because no archetype authors them.
- `ArchetypeStats.ArmorClass`, `AttackBonus`, `TradeSkill` and `TradeGold` are **reserved**: authored on the row, never projected, and absent from `Enemy` on purpose (mitigation is Constitution, no attack roll exists, shop fields have no spawn-path consumer). `ArchetypeStatsProjectionClassificationTests` (`RealmEngine.Data.Tests`) pins the split by reflection — a new stat that is neither projected nor reserved fails, and a reserved stat appearing on `Enemy` fails.
- Gold is the one authored value that is a range: `MapToModel` rolls `[GoldRewardMin, GoldRewardMax]` **inclusive** (`Next(low, high + 1)`) once per materialised template, from an `IRandomSource? randomSource = null` ctor parameter (defaults to `SystemRandomSource.Shared`). A half-authored range collapses to the authored bound; both unset means no draw and the template default survives (the three NPC archetypes author no gold).
- The roll is not repeated on later reads: `EfCoreEnemyInstanceStore` stores `SpawnedEnemy.GoldReward` on the `EnemyInstances` row and every read takes it from there. `Enemy.Template.GoldReward` on a re-read is a fresh roll and is *not* what the player is awarded.
- `ActorPoolResolver` no longer owns a bare `new Random()`: its third ctor parameter is `IRandomSource? randomSource = null`, so roster size, selection and the repository's gold roll all come from the one DI-registered binding and a seed makes a whole spawn reproducible.
- Tests: `EfCoreEnemyRepositoryStatProjectionTests` (`RealmEngine.Data.Tests`, 14 facts, mapping + attributes + inclusive/seeded gold), `ArchetypeStatsProjectionClassificationTests` (`RealmEngine.Data.Tests`, projection contract + reserved list) and `SpawnedEnemyStatProjectionTests` (`Veldrath.Server.Tests`, 5 facts, real seeded catalog through `ActorPoolResolver`). The latter pins 20 HP / 3 damage / 8 XP / 1–3 gold and attributes 4/10/2/6 for `giant-rat`, 55 / 7 / 40 / 5–15 for `bandit-ruffian` and 900 / 42 / 1200 / 150–300 with attributes 30/12/22/26 for `fire-ancient`.

### PowerDto.School Is the Raw Entity School (no tradition mapping)
- `EfCorePowerRepository` returns `Power.School` directly (e.g. `"fire"`, `"arcane"`). No tradition mapping is applied.
- The old `EfCoreSpellRepository.ParseTradition` logic (mapping `"fire"` → `MagicalTradition.Primal` → `"Primal"`) was NOT carried over to `EfCorePowerRepository`. `SpellDto` is gone; `PowerDto.School` is raw.
- Tests asserting on `PowerDto.School` should check raw values (`"fire"`, `"arcane"`, etc.), not broad traditions.

### QuestDto — Partially-Unmapped Fields
- `EfCoreQuestRepository.MapToModel` does **not** set `QuestType` or `Difficulty` on the shared `Quest` model. Those fields default to empty string. Only `Slug`, `Title` (= DisplayName), `DisplayName`, and `RarityWeight` are populated from the entity.

### WeaponDto / ArmorDto / MaterialDto (session-19)
- `WeaponDto(Slug, DisplayName, TypeKey, WeaponType, RarityWeight)` added to `Veldrath.Contracts/Content/ContentContracts.cs`
- `ArmorDto(Slug, DisplayName, TypeKey, ArmorType, RarityWeight)` added to same file — `ArmorType` is `Item.ArmorClass` from the shared model (mapped from `Armor.ArmorType` entity field)
- `MaterialDto(Slug, DisplayName, MaterialFamily, RarityWeight)` added to same file — `RarityWeight` is cast from `MaterialEntry.RarityWeight` (float) to int
- `GET /api/content/weapons`, `/api/content/weapons/{slug}` — backed by `IWeaponRepository`
- `GET /api/content/armors`, `/api/content/armors/{slug}` — backed by `IArmorRepository`
- `GET /api/content/materials`, `/api/content/materials/{slug}` — backed by `IMaterialRepository`
- All 6 routes are anonymous (no auth required)
- `ContentEquipmentEndpointTests.cs` — 12 integration tests in `Veldrath.Server.Tests/Features/`

### Goal-3 New Content Types (session-20)

**Shared models added** (`RealmEngine.Shared/Models/`):
- `OrganizationEntry(Slug, DisplayName, TypeKey, OrgType, RarityWeight)`
- `WorldLocationEntry(Slug, DisplayName, TypeKey, LocationType, RarityWeight, int? MinLevel, int? MaxLevel)`
- `DialogueEntry(Slug, DisplayName, TypeKey, string? Speaker, RarityWeight, List<string> Lines)`
- `ActorInstanceEntry(Slug, DisplayName, TypeKey, Guid ArchetypeId, int? LevelOverride, string? FactionOverride, RarityWeight, string? LocationSlug = null)` (A2 added `LocationSlug`, trailing)
- `MaterialPropertyEntry(Slug, DisplayName, TypeKey, string MaterialFamily, float CostScale, RarityWeight)`
- `TraitDefinitionEntry(string Key, string ValueType, string? Description, string? AppliesTo)` — NOT a ContentBase model, no `IsActive`, no `Slug`

**Interfaces** in `RealmEngine.Shared/Abstractions/` and **EfCore repos** in `RealmEngine.Data/Repositories/` added for all 6.

**TraitDefinition special case**:
- Entity has no `IsActive` — `GetAllAsync()` has no `Where(IsActive)` predicate
- Identifier is `Key` (not `Slug`) — `GetByKeyAsync(key)` not `GetBySlugAsync`
- `GetByAppliesToAsync(entityType)` returns rows where `AppliesTo == "*"` OR `AppliesTo.Contains(entityType)` — both wildcards AND partial matches

**Server routes** (all in `ContentEndpoints.cs`):
- `GET /api/content/organizations[?orgType=]`, `/api/content/organizations/{slug}`
- `GET /api/content/world-locations[?locationType=]`, `/api/content/world-locations/{slug}`
- `GET /api/content/dialogues[?speaker=]`, `/api/content/dialogues/{slug}`
- `GET /api/content/actor-instances[?typeKey=]`, `/api/content/actor-instances/{slug}`
- `GET /api/content/material-properties[?family=]`, `/api/content/material-properties/{slug}`
- `GET /api/content/traits[?appliesTo=]`, `/api/content/traits/{key}` (key not slug)

**Core handlers** added (each with filter + validator):
- `GetOrganizationCatalogQuery(OrgType?)` → `IOrganizationRepository.GetByTypeAsync/GetAllAsync`
- `GetWorldLocationCatalogQuery(LocationType?)` → `IWorldLocationRepository.GetByLocationTypeAsync/GetAllAsync`
- `GetDialogueCatalogQuery(Speaker?)` → `IDialogueRepository.GetBySpeakerAsync/GetAllAsync`
- `GetActorInstanceCatalogQuery(TypeKey?)` → `IActorInstanceRepository.GetByTypeKeyAsync/GetAllAsync`
- `GetMaterialPropertyCatalogQuery(Family?)` → `IMaterialPropertyRepository.GetByFamilyAsync/GetAllAsync`
- `GetTraitCatalogQuery(AppliesTo?)` → `ITraitDefinitionRepository.GetByAppliesToAsync/GetAllAsync`

**Session-20 also completed Goals 1+2** (11 previously-missing catalog query handlers):
- `AbilityCatalog`, `EnemyCatalog`, `NpcCatalog`, `QuestCatalog`, `RecipeCatalog`, `LootTableCatalog`, `SpellCatalog`, `SkillCatalog`, `MaterialCatalog`, `WeaponCatalog`, `ArmorCatalog`
- `WeaponCatalog` and `ArmorCatalog` have no filter param (no filter method on `IWeaponRepository`/`IArmorRepository`)
- `MaterialCatalog` wraps single `Family` string in `[request.Family]` collection expression for `IMaterialRepository.GetByFamiliesAsync(IEnumerable<string>)`
- `QuestCatalog` requires `using SharedQuest = RealmEngine.Shared.Models.Quest;` alias in test file — namespace `RealmEngine.Core.Features.Quest` conflicts with model type name

### DatabaseSeeder — Items & Enchantments (session-18)
- `SeedItemsAsync` seeds 17 items (consumable×4, crystal×2, gem×3, rune×3, essence×3, orb×2) using `I()`, `ISt()`, `ITr()` factory helpers.
- `SeedEnchantmentsAsync` seeds 9 enchantments (weapon×4, armor×3, any×2) using `EE()`, `ESt()`, `ETr()` factory helpers.
- `SeedContentRegistryAsync` also registers Items (`items/general`) and Enchantments (`items/enchantments`).

### ShopEconomyService / Shop Handler Tests
- Pass `null!` for the DbContextFactory: `new ItemDataService(null!, NullLogger<ItemDataService>.Instance)` is safe because `LoadCatalog` catches the resulting exception and returns `[]`.
- Pre-populate a merchant's inventory via `shopSvc.GetOrCreateInventory(merchant)` then mutate `inventory.CoreItems`, `inventory.DynamicItems`, or `inventory.PlayerSoldItems` directly before passing to the handler under test.
- `CalculateSellPrice` for a Common item with `Price=100` and no modifier traits returns exactly 100 (quality multiplier = 100/100 = 1.0).

### BudgetHelperService / BudgetCalculator Tests
- `new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance)` works directly — no DI required.
- Default `BudgetConfig` values: `EnemyLevelMultiplier=5.0`, `BossMultiplier=2.5`, `EliteMultiplier=1.5`. So `CalculateBaseBudget(10)` = 50.

### Services Requiring EF Core InMemory (cannot unit-test without DB)
- `ItemDataService` — calls `_dbFactory.CreateDbContext()` in `LoadCatalog()`
- `CategoryDiscoveryService` — queries 11+ EF Core DbSets in `Initialize()`
- `MaterialPoolService` — calls `_dbFactory.CreateDbContextAsync()` in every public method
- `BudgetItemGenerationService` — depends on `MaterialPoolService` (transitively EF Core)

## Combat Loop Foundation — Phase 0a–0c (2026-03-30)

### Phase 0a — ICombatSettings
- `ICombatSettings` interface in `RealmEngine.Shared/Abstractions/ICombatSettings.cs`
  - Properties: `PlayerDamageMultiplier`, `EnemyDamageMultiplier`, `EnemyHealthMultiplier`, `GoldXPMultiplier`, `IsPermadeath`
- `DifficultySettings` already implemented `ICombatSettings`; confirmed all 5 properties covered
- `NormalCombatSettings` + `HardcoreCombatSettings` static impls in `RealmEngine.Shared/Models/`
- `CombatService` primary ctor: `(ICombatSettings, IMediator, PowerDataService, ILogger, ILoggerFactory, ItemGenerator? = null)`
- Adapter ctor `(ISaveGameService, ...)` delegates to primary for single-player backward compat
- `AttackEnemyCommandHandler` reads multipliers from `ICombatSettings`

### Phase 0c — ZoneLocation ActorPool
- `ActorPoolEntry` class in `RealmEngine.Data/Entities/Content/ZoneLocation.cs`: `string ArchetypeSlug`, `int Weight = 1`
- `ZoneLocation.ActorPool: IList<ActorPoolEntry>` — `jsonb` column, owned via `OwnsMany(x => x.ActorPool).ToJson("ActorPool")` in `ContentModelConfiguration.cs`
- `ActorPoolEntry(string ArchetypeSlug, int Weight)` positional record added to `ZoneLocationEntry` (`RealmEngine.Shared/Models/ZoneLocationEntry.cs`)
- `ZoneLocationEntry` last positional parameter: `IReadOnlyList<ActorPoolEntry>? ActorPool = null`
- `EfCoreZoneLocationRepository.MapToModel`: `.Select(e => new ActorPoolEntry(e.ArchetypeSlug, e.Weight)).ToList()`
- Content migration: `20260330000004_AddZoneLocationActorPool` (adds `ActorPool jsonb` default `'[]'`)
- `ActorPoolEntry` type is in scope as `RealmEngine.Data.Entities.ActorPoolEntry` (entity) vs `RealmEngine.Shared.Models.ActorPoolEntry` (shared record) — do NOT prefix with `Models.` in the repository file; the shared model is already in scope via using

## Known Open Items (as of March 2026)

### RealmEngine Core Abstractions
- `IGameStateService` interface extracted to `RealmEngine.Core/Abstractions/IGameStateService.cs`
- All 12 handlers/services (Exploration, Death/Respawn) depend on `IGameStateService` (not concrete `GameStateService`)
- `GameStateService` depends on `ISaveGameService` (not concrete `SaveGameService`)
- All test mocks use `Mock<IGameStateService>`; `FakeGameStateService` implements `IGameStateService` directly

### P3 Stubs — see [veldrath-memory.md](veldrath-memory.md) for full status

### Veldrath — Hub Architecture, Blob Schema, P3/P4 Status

> Full details in [veldrath-memory.md](veldrath-memory.md). Summary:

- Server hub commands follow the **blob pattern**: read `Character.Attributes` JSON, mutate, save. Never call Core handlers (type mismatch).
- Wired bridges: `RestAtLocation`, `AllocateAttributePoints`, `UseAbility` (`GainExperience` and `TakeDamage` were retired from the hub 2026-11-23; their commands are dispatched server-side only)
- **P3 stubs: closed.** `CharacterSelectViewModel.ServerUrl` is no longer hardcoded — the desktop client
  reads `Server:BaseUrl` and the old `ScopedSession`/`TravelToNode` stubs were wired to real hub commands.
- **P4 XML docs: closed** (re-audited 2026-10-24). `IPlayerAccountRepository`, `IZoneRepository`, and
  `IZoneSessionRepository` all carry `<summary>` docs, and CS1591 is a compile error, so an undocumented
  public member cannot land in the first place.
======= REPLACE

### Avalonia Thread Affinity in Client Tests
- `MapEdgeViewModel.ComputeStyle` creates `SolidColorBrush` (inherits `AvaloniaObject`) which requires the Avalonia dispatcher thread.
- Any test that constructs `MapEdgeViewModel` (directly or indirectly via `MapViewModel`) will throw `InvalidOperationException: Call from invalid thread` when run after `[AvaloniaFact]` tests that initialized the dispatcher on a different thread.
- **Fix**: use `[AvaloniaFact]` (not `[Fact]`) for any test that creates `MapEdgeViewModel` or a `MapViewModel` that loads zone-exit edges.
- Tests that create `MapViewModel` but with no zone connections (so no `MapEdgeViewModel` is created) are safe to remain as `[Fact]`.

### ActorClassDto Changed (2026-03-19)
- `ActorClassDto` in `Veldrath.Contracts` was updated to add `HitDie` (int), `PrimaryStat` (string), and `RarityWeight` (int) parameters
- This was a breaking change that broke `FakeServices.cs` and `CharacterSelectViewModelTests.cs` in Client.Tests
- Fix: update all `new ActorClassDto(slug, name, typeKey)` calls to `new ActorClassDto(slug, name, typeKey, hitDie, primaryStat, rarityWeight)`

### Session-17 Integration Test Notes
- `ContentTypedEndpointsFixture` seeds ActorClass, Species, Background, Skill, 2×Enchantment (different TargetSlot), Ability, Item into `ContentDbContext` — all routes under test resolve through the SQLite in-memory DB
- `Enchantment` in the fixture = `RealmEngine.Data.Entities.Enchantment` (not Shared.Models) — resolved unambiguously because the test file does NOT import `RealmEngine.Shared.Models`
- `EfCoreAbilityRepository.MapToModel` sets `Type = ParseAbilityType(entity.AbilityType, ...)` — seeding with `AbilityType = "active"` produces `AbilityTypeEnum.Offensive`; the endpoint DTO includes this as a string; tests do NOT assert the AbilityType string value
- `Item.TotalRarityWeight` is NOT set by `EfCoreItemRepository.MapToModel` (maps Slug/Name/TypeKey/Weight/Price/StackSize/IsStackable only) — `ItemDto.RarityWeight` will be 0 for all seeded items in tests; avoid asserting RarityWeight in item DTO tests
- `InMemoryHallOfFameRepository` already has a dedicated test file: `RealmEngine.Data.Tests/Repositories/InMemoryHallOfFameRepositoryTests.cs` — do NOT add it to `InMemoryStubRepositoryTests.cs`
- `InMemoryEquipmentSetRepository` is NOT a stub — it returns 5 hardcoded sets (Dragonborn, Shadow Assassin, Arcane Scholar, Iron Guardian, Stormcaller). Test against non-empty count.
- `ContentEndpoints.cs` intentionally uses `ContentDbContext` directly for `/classes`, `/species`, `/backgrounds`, `/skills` — those handlers map entity-specific fields (HitDie, PrimaryStat, GoverningAttribute, MaxRank) not present on shared models. This is by design, not a bug.
- `EfCoreMaterialRepository` and `EfCoreArmorRepository` / `EfCoreWeaponRepository` take only `ContentDbContext` (no logger param) — do NOT pass `NullLogger<T>.Instance`
- `HallOfFameEntry` is in `RealmEngine.Shared.Models`, NOT `RealmEngine.Data.Entities` — when using it in Data.Tests (which imports both namespaces), use alias: `using HallOfFameEntry = RealmEngine.Shared.Models.HallOfFameEntry;`
- `Enchantment` and `Item` exist in BOTH `RealmEngine.Data.Entities` and `RealmEngine.Shared.Models` — always qualify or alias when both namespaces are imported
- `ContentEndpoints.cs` enchantment endpoint uses `ContentDbContext` directly (entity has `TypeKey`); item endpoint uses `IItemRepository` (shared model has `TypeKey` mapped)
- `Item.TypeKey` on the shared model can be null — use `?? string.Empty` when mapping to `ItemDto.TypeKey`
- `GetSpeciesQuery` handler namespace is `RealmEngine.Core.Features.Species.Queries` (feature folder named `Species`)
- `EfCoreHallOfFameRepository` methods are synchronous (no async) — tests use `using var db` not `await using var db`

### AvailableClasses Test Gotcha
- `CharacterSelectViewModel.LoadAsync` fires-and-forgets on construction; `FakeContentService.GetClassesAsync()` completes synchronously (Task.FromResult)
- So `LoadAsync` replaces `AvailableClasses` before the test's first assertion if a non-empty `FakeContentService` is used
- Test for fallback list: pass `new FakeContentService { Classes = [] }` + `await Task.Delay(50)` to let fire-and-forget complete
- Test for catalog-populated list: pass classes with `Task.Delay(50)` wait

### ISpeciesRepository (added session-13)
- `ISpeciesRepository` interface in `RealmEngine.Shared/Abstractions/`
- `EfCoreSpeciesRepository` in `RealmEngine.Data/Repositories/`
- `RealmEngine.Shared/Models/Species.cs` shared model (Slug, DisplayName, TypeKey, RarityWeight)
- Registered in `Veldrath.Server/Program.cs` as `AddScoped<ISpeciesRepository, EfCoreSpeciesRepository>()`
- DTO: `SpeciesDto` in `Veldrath.Contracts` — browse-list projection only (no stats/traits)

### EfCoreBackgroundRepository Legacy Cleanup (session-13)
- Removed dead-code `:` split in `GetBackgroundByIdAsync` — now queries directly by bare slug
- Removed `GetBackgroundByIdAsync_StripsPrefixedSlug` test (was testing dead code)

### CharacterContracts.cs XML Docs (session-13)
- Added full XML `<summary>` + `<param>` docs to `CreateCharacterRequest` and `CharacterDto`
- `ClassName` param doc states: stores the class display name (e.g. "Warrior", "Mage")

### EfCoreEquipmentSetRepository Tests (session-13)
- All 3 public methods tested (synchronous — no async): `GetAll`, `GetById`, `GetByName`
- No logger constructor arg — just `new EfCoreEquipmentSetRepository(db)`

### EfCoreCharacterClassRepository Notes
- `MapToModel` hardcodes `IsSubclass = false` and `ParentClassId = null` for all rows
- `GetClassesByType(classType)` filters by `Id.StartsWith("{classType}:")` — Id format = `"{typeKey}:{displayName}"`
- Uses sync-over-async pattern with `SemaphoreSlim` cache — test each method on a fresh `ContentDbContext` (separate `CreateDbContext()` call) to avoid cache bleed between tests
- Implements `IDisposable` — call `repo.Dispose()` in tests if creating multiple repos on same db

### EfCoreNamePatternRepository Location
- Lives in `RealmEngine.Core/Repositories/` (NOT in `RealmEngine.Data/Repositories/`)
- Tests in `RealmEngine.Core.Tests/Repositories/EfCoreNamePatternRepositoryTests.cs`

### IItemRepository (added session-14)
- Interface in `RealmEngine.Shared/Abstractions/`, impl `EfCoreItemRepository` in `RealmEngine.Data/Repositories/`
- Uses `ContentDbContext.Items` — entity `Item` has `ItemType` (not TypeKey) for category filtering
- Methods: `GetAllAsync`, `GetBySlugAsync`, `GetByTypeAsync(itemType)` — itemType is lowercased before query
- MapToModel maps: Slug, Name, TypeKey, Weight (float→double), Price (Value), StackSize, IsStackable

### IEnchantmentRepository (added session-14)
- Interface in `RealmEngine.Shared/Abstractions/`, impl `EfCoreEnchantmentRepository` in `RealmEngine.Data/Repositories/`
- Uses `ContentDbContext.Enchantments` — entity `Enchantment` has `TargetSlot` column
- Methods: `GetAllAsync`, `GetBySlugAsync`, `GetByTargetSlotAsync(targetSlot)` — lowercased before query

### INodeRepository / EfCoreNodeRepository (added session-14)
- Interface `INodeRepository` already existed in `RealmEngine.Shared/Abstractions/` (8 methods, all mutating + querying)
- EF entity `HarvestableNodeRecord` in `RealmEngine.Data/Entities/HarvestableNodeRecord.cs` — PK = `NodeId` (string)
- `EfCoreNodeRepository` uses **`GameDbContext`** (not ContentDbContext) — nodes are runtime game state, not catalog content
- Migration `AddHarvestableNodes` in `RealmEngine.Data/Migrations/GameDb/` (generated with dotnet ef)
- `GetNearbyNodesAsync` ignores `radius` (same as InMemory version) — real spatial queries would need PostGIS
- `SaveNodeAsync` is an upsert: insert if NodeId not present, update mutable fields if found
- Tests are in `RealmEngine.Data.Tests/Repositories/EfCoreGameRepositoryTests.cs`

### ActorClassDto XML Docs (session-14)
- Added full `<param>` docs for all 6 positional parameters of `ActorClassDto`
- TypeKey doc: "DB category key for this class family (e.g. \"warriors\", \"casters\"). Not a content-reference prefix."


---

### New Patterns Introduced (Post-Session-31)

#### Moderation Options Pattern
- `Veldrath.Server/Settings/ModerationOptions.cs` binds `IOptions<ModerationOptions>` from `appsettings.json` `"Moderation"` section
- Properties: `AutoBanWarnThreshold` (default 3), `MaxChatMessageLength` (default 500)

#### Version Compatibility Enforcement
- `Veldrath.Server/Settings/VersionCompatibilitySettings.cs` with `MinCompatibleClientVersion`
- Value stamped at Release publish time via MSBuild target

#### Rate Limiting by Policy
- 5 named policies in `Program.cs`: `foundry-writes`, `admin-actions`, `auth-attempts` (per-IP), `jwt-renewal` (per-IP), `hub-commands`
- Uses ASP.NET Core rate limiting with fixed-window and partitioned (per-IP) policies

#### Migration Repair Strategy
- `RepairStaleMigrationsAsync` in `Program.cs` handles squashed migrations gracefully: drops dev DB, throws in production
- Used after consolidating 10+ old migrations into single `InitialCreate` per DbContext

#### ConnectionGuard Pattern
- `_connectionGeneration` counter in `ServerConnectionService` prevents stale event handlers from corrupting state
- `ConnectAsync` guarded against `Connecting` and `Reconnecting` states (previously only `Connected` was guarded)
- `DisconnectAsync` increments generation before stopping, preventing spurious `ConnectionLost` events

#### Shared Auth Library Pattern
- `Veldrath.Auth` contains shared `IVeldrathAuthApiClient` and `VeldrathAuthApiClient`
- `Veldrath.Auth.Blazor` contains `AuthStateServiceBase` for Blazor auth state management
- Both `RealmFoundry` and `Veldrath.Web` inherit from these shared classes instead of duplicating

#### OAuth Provider Conditional Registration
- Discord, Google, Microsoft account providers registered only when credentials present in config
- Safe for CI/dev environments without OAuth app credentials

#### Metrics & Monitoring Stack
- Prometheus + Grafana via `prometheus-net`, `prometheus-net.DotNetRuntime`, `prometheus-net.AspNetCore`
- 4 Grafana dashboards provisioned in `config/grafana/dashboards/`
- `UseHttpMetrics()`, `DotNetRuntimeStatsBuilder` in `Program.cs`

#### Security Headers Middleware
- Inline middleware in `Program.cs` sets: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`, `Permissions-Policy` (camera=(), microphone=(), geolocation=()), `Content-Security-Policy` (restrictive with Google Fonts allowance)

#### Caddy Reverse Proxy Pattern
- Production deployment uses Caddy for TLS termination
- Forwarded headers middleware (`UseForwardedHeaders`) configured in `Program.cs`
- `Auth:CookieDomain` production guard prevents cookie misconfiguration

### Known Gotchas (Post-Session-31)

- **ChatCommandInfoDto lives in `Veldrath.Contracts/Chat/`** — not in the engine. Chat contracts are server-client only
- **Moderation commands use SignalR chat infrastructure** — they do NOT go through the Hub→MediatR bridge. They're handled directly in `GameHub.cs`
- **Dev endpoints use minimal API endpoints** — NOT through SignalR. They're in `DevEndpoints.cs` with `IsDevEnvironment()` guard
- **Auth library has its own test project** — `Veldrath.Auth.Tests` with 2 test files (436+ lines). Must not be overlooked when running full test suite
- **Editorial endpoints use separate DbContext** — `EditorialDbContext` in `Veldrath.Server/Data/` with its own migration history. Not to be confused with `ContentDbContext` or `GameDbContext`
- **Roo skills are project-specific** — defined in `.roo/skills/` (catalog in `.roo/skills/README.md`); the former nested `web/.roo/` skills were merged into `web-development` and `web-ui`. They are NOT auto-loaded; must be explicitly invoked
- **Three DbContext separation expanded** — now 4: `ApplicationDbContext` (server auth/ops), `GameDbContext` (game state), `ContentDbContext` (catalog), `EditorialDbContext` (editorial content)

### RCL unification — Map & Hotbar (Session-42, 2026-07-01) — historical

The `Veldrath.GameClient.Components` Razor class library (region-map page, game sidebar, action-bar
hotbar, `wwwroot/css/game.css`) and its 14 tests were **deleted** with the Blazor decommission. The
equivalent UI now lives in the SvelteKit app under `web/client/src/lib/game/` (map panel, left
sidebar, hotbar inside the game shell) and in the Avalonia client's `GameViewModel`. There are no
`.razor` files left in the tree — do not look for them.

### Open Items (re-audited Session-47, 2026-10-22)

- **`PendingLinkCleanupService` no longer exists** — the pending-link tables, `AccountLinkService`, and the whole Identity auth surface were deleted in W12 (2026-10-25). Better Auth owns OAuth linking now.
- **Wiki implementation status tracker** — refreshed Session-47 (`wiki/Engine-Implementation-Status.md`); zone/region wiki pages now reference map graph ids instead of TMX files.
- **Tile-era hub tests are obsolete** — `GetRegionMap`, `MoveOnRegion`, `ExitZone`, and `ChangeRegion` were removed with the tile layer; do not add success-path tests for them.
- **Web character creation is session-based** — the RCL `CreateCharacter` wizard already calls `/api/character-creation/sessions/*` for class, species, background, name, attributes, equipment, and finalize. The older "web uses a minimal 3-step flow" plan is superseded.
- **RCL test gaps** — historical: the Blazor RCL and its test project were deleted in Phase 6; web UI coverage now lives in `web/client/tests`.
- **Historical fixes kept for context** — all CharacterCreationSessionEndpointTests passing (Session-41), ExternalAuth SecurityStamp fix, EnemyAiService disposal guard, achievement triggers, settings cleanup, Discord embed tests.
### Map graph authoring tools (Session-47, 2026-10-22)

- **JSON authoring format** — `RealmEngine.Data/Authoring/MapGraphJsonSerializer.cs` wraps
  `MapGraphFile(FormatVersion=1, ExportedAt, Maps)`; camelCase, indented, string enums,
  deterministic ordering. Import throws `InvalidDataException` for empty/malformed JSON,
  unsupported versions, and duplicate map ids.
- **Map graph editor (retired)** — `RealmForge/Services/MapGraphEditorService.cs` used to sync
  nodes/edges against `ContentDbContext`. `RealmForge` was deleted in Milestone B; the map graph
  is now authored as data through the Foundry write path. `Map.Version` starts at 1 and increments
  per save.
- **EF InMemory test gotcha** — never generate the InMemory database name inside the
  `AddDbContext` options lambda; it executes per scope and creates a fresh database per
  context. Capture the name once.

### Inventory & starter equipment (Session-52, 2026-10-23)

- **Equipped items live in both blobs** — character creation adds starter items to
  `InventoryBlob` and also records their refs in `EquipmentBlob`. Since Session-70
  `GetInventoryHubCommandHandler` no longer filters equipped refs out of the list: it returns every
  owned item flagged with `IsEquipped` (equipment-blob-only starters are appended), because the web
  character sheet renders one bag list with an "Equipped" badge.
- **InventoryLoaded wire shape (Session-70)** — `{ CharacterId, Items, Equipped, OpenOverlay }`.
  Both sides are flat `InventoryItemView` records (`ItemRef, Name, Description, Rarity, Type, TypeKey,
  Slot, Quantity, StackSize, IsStackable, IsEquipped, Price, Weight, Durability`) — **rarity and type
  are strings**, because `ItemRarity`/`ItemType` enums serialise as numbers over SignalR (that bug made
  `itemSubtitle` print `2` and killed every `rarity-*` colour). `Items` includes equipped entries;
  `Equipped` is slot → view. `web/client/src/lib/game/types.ts` mirrors these fields and
  `payload-contract.test.ts` fails on drift.
- **ItemEquipped wire shape** — `{ CharacterId, Slot, ItemRef, AllEquippedItems, Equipped }` where
  `Equipped` is now slot → `InventoryItemView`. `GameHub.EquipItem` follows the event with a
  caller-only `InventoryLoaded` snapshot so bag/equipment state refreshes atomically.
- **`EquipItemHubCommand.Slot` is optional (Session-70)** — when omitted the handler resolves it from
  the catalog item via `EquipmentSlots.ForItem` (`Weapon`→`MainHand`, `Shield`/`OffHand`→`OffHand`,
  `Helmet`→`Head`, `Chest`, `Legs`, `Boots`→`Feet`, `Ring`, `Necklace`→`Amulet`, everything else
  `null` = not equippable). An explicit but unknown slot still returns `Invalid equipment slot '…'`.
- **CharacterSelected includes `Equipment`** — slot→ref map seeded into the HUD, so starter
  gear is visible before the player opens the inventory.
- **Canonical slot names** — `MainHand`, `OffHand`, `Head`, `Chest`, `Legs`, `Feet`, `Ring`,
  `Amulet`. Character creation maps engine properties to these (`Helmet`→`Head`,
  `Boots`→`Feet`, `Necklace`→`Amulet`).
- **No shield item in the seeded catalog** — `CreateCharacterHandler` synthesizes the fallback
  `wooden-shield` (`ItemType.Shield`, `TypeKey`/`ArmorType` = `shield`) when
  `IncludeShield` is true. Shield matching also accepts `TypeKey == "shield"`, not just
  `ArmorType`.
- **`EfCoreItemRepository.MapToModel`** maps `Item.Type` from the persisted `ItemType`
  discriminator (`weapon` / `armor`) plus the category (`shield`, `head`, `chest`, `legs`,
  `feet`, etc.). This drives the RCL equip button and slot routing.

### Web-first RCL actions (Session-53, 2026-10-23)

- The browser game client (SvelteKit) is the primary experience; see `docs/client-ui-architecture.md`.
  Desktop/Avalonia follows web decisions.
- **Hub request shapes matter.** `VisitShop` requires `{ ZoneId }`, `RestAtLocation` requires
  `{ LocationId, CostInGold }`, and `NavigateToLocation` requires `{ LocationSlug }`. Sending a
  bare string or no argument fails SignalR binding before the hub body runs.
- `ExitZone` no longer exists; use `LeaveZone` (no args) and then `RequestMapGraphAsync`.
- The `/Game/Map` route does not exist; the cardinals map is inline on `/Game/Play`.
- `ZoneEntered` carries `HasInn` / `HasMerchant`; `GameStateService.ApplyZoneEntered` stores them
  in `ZoneState` so `LeftSidebar`/`GameFooter` render shop/rest actions.
- `GameCombat` owns combat actions; do not add an unbound `ActionBar` in `GameFooter`.
- **Shop hub method signatures** — `BuyItem(string itemRef)`, `SellItem(string itemRef)`, and
  `DropItem(string itemRef)` take a single string, not an object. `GetShopCatalog()` takes no
  arguments. `ShopVisited` only announces the merchant; clients must send `GetShopCatalog`.
- **ShopCatalog payload** — server sends both `Items` (Avalonia `ShopItemDto`: ItemRef,
  DisplayName, BuyPrice, SellPrice) and `Catalog` (RCL `ShopItemEntry`: Slug, Name, Rarity,
  Price). Keep both until the desktop client moves to the shared payload.
- **Transaction refresh** — after buy/sell/drop the server sends a background `InventoryLoaded`
  snapshot with `OpenOverlay = false`; clients update bag/equipment/gold without switching panels.
  Explicit `GetInventory` uses `OpenOverlay = true`.
- **No `UseItem` hub method** — do not render a Use action in the RCL inventory until one exists.

### Cardinal map layout (Session-55, 2026-10-23)

- **Post-tile layout** — `CardinalMap` uses 152×60 node cards in 160×70 cells (8px/10px gutters)
  with 8px canvas padding, a bounded scroll viewport, and arrow connector labels
  (`↑/↓/→/←`). Do not reintroduce tile-sized cells, tile backgrounds, or `--vds-tile-*` tokens.
- **Authored coordinates are density-gated** — dense authored grids are kept (compacted to
  ordered indices); sparse TMX-era layouts (e.g. 8 nodes spread across a 64×40 tile rectangle)
  are discarded and replaced by a compact edge-driven layout seeded at the entry node. This is
  the fix for large empty gaps between nodes.
- **Connectors run edge-to-edge** — horizontal/vertical lines start at facing card edges, not
  centres; L-shaped routes are used only when authored positions are not cardinally aligned.
- **Keyboard navigation follows edges** — arrow keys first look for a traversable edge in that
  direction (including incoming edges), then fall back to the nearest spatial node.
- **Direction-aware placement** — computed nodes are reserved in the edge's cardinal half-plane
  (`ReserveDirectional`), so a north edge never places its target west just because the straight
  cell is occupied. If the straight ray is blocked, adjacent perpendicular offsets are used.
- **Authored layouts must agree with edges** — a dense authored grid is only kept when at least
  80% of checked edges place the target in the direction the edge names; otherwise the compact
  edge-driven layout wins. This prevents arrow labels contradicting the visual layout.
- **L-shaped routes** — when two nodes still end up diagonal, the connector hugs the destination
  card's side in two short segments and hides the direction arrow (no single cardinal direction).
- **Map/detail layout** — the game shell is exactly three columns (left sidebar / center / right
  chat). `.map-layout` stacks the map above `MapDetailPanel` inside the center column; do not
  add another grid column there.

### Test seams and the coverage contract (Session-99, 2026-11-04)

- **Coverage floors are high now** — global 94% line / 81% branch, Core 90/79, Server 94/82,
  Data 98/83, Shared 97/94, Contracts 99/—, Discord 88/91. New product code has to ship with
  tests in the same PR or the gate fails; there is no slack left to absorb untested classes.
- **Discord.Net command modules are testable offline** — `Veldrath.Discord.Tests/Infrastructure/
  DiscordInteractionHarness.cs` emits an internal-access `SocketInteraction` subclass and assigns
  it to `InteractionModuleBase<T>.Context` through the private setter. Use it (do not add a
  production seam) and assert the recorded embed/reply, not just "no throw".
- **Handler catch arms are reachable** with `RealmEngine.Core.Tests/Infrastructure/
  ThrowingInformationLogger.cs` instead of an interface extraction.
- **Prefer seeded `IRandomSource` over retries** — assertions on unseeded `Random.Shared` draws
  are what produced the old 0.41-point coverage oscillation and the `EnemyPowerAIService` flake.

### Content packs — the editable catalogue (2026-11-20)

- **The seeders are insert-only; packs are the writable half.** `RealmEngine.Data/Seeders/*` stays the initial
  catalogue and never updates a row it already sees. `content/<family>/<batch>.yaml` is upserted by
  `Veldrath.ContentPack` (`dotnet run --project Veldrath.ContentPack -- validate content/`, then `import` every
  family directory under `content/` in dependency order — `items`, `loottables`, `archetypes`, `zonelocations`,
  `languages`, because a pack cannot resolve a GUID for a row it creates in the same run), keyed by slug:
  create missing, update changed, leave unchanged untouched. Do not "fix" a seeder's insert-only behaviour to make an
  edit land — author a pack or change the seeded value deliberately. **Name every family directory in the document
  that shows the commands**: an omitted directory is never read, so the run still prints `Import complete: the catalog
  already matched the pack.` and the author concludes their edit landed when the file was never opened.
- **One rule set, three callers.** `RealmEngine.Data/Content/ContentEntityTypes.cs` (moved out of
  `Veldrath.Server/Features/Content/`, now public) is the only type-key → entity-type/validator map, shared by the
  HTTP write path, the pack validator and the startup audit. Adding a content family means adding it there, in
  `ContentValidatorRegistry` and in `ContentSchemaRegistry` — nothing else holds a switch.
- **`ContentPackImporter` compares EF projections, not CLR objects.** JSON-mapped owned values (`Stats`, `Traits`,
  quest objectives) only persist when the tracked owned instance is mutated *in place*: replacing the instance leaves
  EF comparing the old one and the column keeps its stored value. Reload navigations before comparing, and never
  assign an owned navigation on a tracked row.
- **Pack GUID foreign keys are authored as slugs** (`speciesId: human`, `lootTableId: wolf-loot`); the validator
  resolves them through `ContentEntityTypes.FindBySlugAsync`. A pack cannot resolve one for a row it creates in the
  same run.
- **`ci-content.yml` is the guard; `SeededContentValidationTests` is not a substitute** — one reads `content/`
  YAML, the other reads the C# seeders, and `content/**` never triggered `ci-server.yml`.
- **A `ContentPackFile` may have an empty `Family`.** A file in the pack root (no family directory, no `family:`
  header), an unknown family directory, or a file that failed before its family was read comes back with an empty
  family and no entries — the reader has already reported which (`pack-family-directory-required`,
  `pack-family-unknown`, a file-level rule). Anything walking `read.Files` must skip a family
  `ContentEntityTypes.TryResolve` rejects: `ContentReferenceCatalog.WithContent` throws on a blank content type,
  which is how `validate <family-dir>` used to die with a bare parameter exception instead of naming the file.
- **A shelf edit is a data migration; stock has no pack family.** `ZoneStockItems` is
  deliberately absent from `ContentEntityTypes` (the 22 keys at `ContentEntityTypes.cs:37-60`), from Foundry and from
  the importer: a row is a `(ZoneId, ItemSlug)` pair of soft string references with no slug and no foreign key
  (`ZoneStockItem.cs:22-31`), and the `Zone` it names lives in `ApplicationDbContext`
  (`Veldrath.Server/Data/Entities/Zone.cs:7`, `ApplicationDbContext.cs:34`) — a context the importer never opens.
  **The constraint is the context, not the database**: there is one Postgres database, and `Program.cs:95` hands the one
  `DefaultConnection` to `ApplicationDbContext` (`:97-98`), `GameDbContext` (`:103-104`), `EditorialDbContext`
  (`:107-108`) and the `ContentDbContext` factory (`:240`) — but an import writes `ContentDbContext` only, and
  `ApplicationDbContext` is live game data an import must never touch. Shape rules a family out too: the importer's
  create/update decision is `FindBySlugAsync` on a `ContentBase` (`ContentPackImporter.cs:119`,
  `ContentEntityTypes.cs:114-124`) and its child reconciliation hangs off a parent navigation
  (`ContentPackImporter.cs:275-306`), so there is nothing to key a stock row by. (The other tables that carry no pack
  family are ruled out by that same shape test, not by that reason: none of them is a `ContentBase`, so
  `FindBySlugAsync` has nothing to match — `ZoneLocationConnection`, a join table keyed by `FromLocationSlug`/
  `ToLocationSlug` (`ZoneLocationConnection.cs:14`, `:20`), and the map graph's `Map`/`MapNode`/`MapEdge`, a map
  document, its nodes and its edges, keyed by authored string ids rather than slugs. The
  support tables — `ContentRegistry`, `TraitDefinitions`, `NamePatternSets` at `ContentDbContext.cs:19`, `:21`, `:89` —
  and `GameConfig` are not `ContentBase` rows either; they are vocabulary and configuration rather than per-entity
  content, so no pack family addresses them.)
  Decided in `t_3231a2ea`:
  `ZoneStockSeeder.Authors()` is the truth, the seeder stays insert-only, and a row the shelf no longer lists is
  removed by a data migration (`DELETE … WHERE "ZoneId" = … AND "ItemSlug" = …`, empty `Down` because the `Id` is
  generated on insert) in `RealmEngine.Data/Migrations/`. Adding a row needs no migration — the seeder inserts what is
  missing on the next start. The procedure and the verification query live in
  [`docs/content-packs.md`](../../docs/content-packs.md). **Do not make the seeder reconcile**: it would silently
  delete a row another authoring surface owns, which is the failure this decision exists to prevent.

### Placed actor instances (A2, 2026-11-20)

- **Instances are placed by `ActorInstance.LocationSlug`** (null = catalogue-only). The spawn path emits every placed
  instance once, then fills from the location's pool to the rolled 2–4; overrides compose onto the archetype's projected
  template at spawn (null = inherit, an absolute number replaces), one gold roll after the pair is composed.
  `ArmorClass`/`AttackBonus`/`PowerPool` are reserved and applied nowhere. `EnemyInstance.InstanceSlug` is why a reloaded
  boss keeps its authored damage: `ToSpawnedAsync` resolves the composed template from the instance, not the archetype,
  while `ArchetypeSlug` stays the identity the unique index and reconciliation use.
