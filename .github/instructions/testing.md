# Testing Conventions

Scope: **ALL 13 test projects** in the solution — [`RealmEngine.Core.Tests`](../../RealmEngine.Core.Tests/), [`RealmEngine.Shared.Tests`](../../RealmEngine.Shared.Tests/), [`RealmEngine.Data.Tests`](../../RealmEngine.Data.Tests/), [`Veldrath.Server.Tests`](../../Veldrath.Server.Tests/), [`Veldrath.Client.Tests`](../../Veldrath.Client.Tests/), [`Veldrath.Web.Tests`](../../Veldrath.Web.Tests/), [`Veldrath.GameClient.Core.Tests`](../../Veldrath.GameClient.Core.Tests/), [`Veldrath.GameClient.Components.Tests`](../../Veldrath.GameClient.Components.Tests/), [`Veldrath.Auth.Tests`](../../Veldrath.Auth.Tests/), [`Veldrath.Assets.Tests`](../../Veldrath.Assets.Tests/), [`Veldrath.Discord.Tests`](../../Veldrath.Discord.Tests/), [`RealmForge.Tests`](../../RealmForge.Tests/), [`RealmFoundry.Tests`](../../RealmFoundry.Tests/).

This file covers every test surface type in the solution: EF Core data tests, MediatR handler tests, Avalonia UI tests, Blazor/bunit component tests, SignalR hub integration tests, and code coverage configuration.

---

## General Testing Principles

### Layout

Every test follows **Arrange / Act / Assert** layout. Keep each test focused on a single behavior.

```csharp
[Fact]
public async Task SomeHandler_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var repository = new InMemoryFooRepository();
    var handler = new FooHandler(repository);

    // Act
    var result = await handler.Handle(new FooCommand("valid"), CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Success.Should().BeTrue();
}
```

### Test Attributes

- **`[Fact]`** — Single-case test with no parameters.
- **`[Theory]`** — Parameterized test. Use `[InlineData]` or `[MemberData]` for inputs.
- **`[AvaloniaFact]`** — Required for any test that touches Avalonia types (dispatcher, brushes, view models with AvaloniaObject dependencies). See [Avalonia / UI Testing](#avalonia--ui-testing).

### Assertions

Use **FluentAssertions** (`Should()`) exclusively:

```csharp
result.Should().NotBeNull();
result.Damage.Should().BeGreaterThan(0);
result.IsCritical.Should().BeTrue();
items.Should().HaveCount(3);
items.Should().ContainSingle(i => i.Slug == "iron-sword");
```

### Mocking / Stubbing

**Prefer `FakeXxx` stub classes** in `Infrastructure/` directories over mocking frameworks. This is a project-wide rule.

- ✅ Create a `FakeFooRepository` or `InMemoryFooService` implementing the interface.
- ❌ Do not reach for Moq/NSubstitute first.

Moq and NSubstitute are available as a **fallback** when stubs aren't practical (e.g., verifying that a method was called exactly once with specific arguments). Hub tests in particular use `Mock<ISender>` for verifying MediatR dispatch — see the [hub-development.md](hub-development.md) testing section.

### File Structure

Test files mirror source structure exactly:

```
RealmEngine.Core/Features/Combat/Commands/AttackEnemy/AttackEnemyHandler.cs
RealmEngine.Core.Tests/Features/Combat/Commands/AttackEnemy/AttackEnemyHandlerTests.cs
```

### Test Project Naming

Every test project is named `{SourceProject}.Tests` and references its corresponding source project.

---

## EF Core / Data Testing

### Provider Selection

| Provider | When to Use | FK Enforcement | Transaction Support | Example Project |
|---|---|---|---|---|
| **EF Core InMemory** | Most unit tests (fast, no real DB) | ❌ No | ❌ No | [`RealmEngine.Data.Tests`](../../RealmEngine.Data.Tests/) |
| **SQLite in-memory** | When SQL semantics matter (FK constraints, unique indexes, complex queries) | ✅ Yes | ✅ Yes | [`Veldrath.Server.Tests`](../../Veldrath.Server.Tests/) |
| **Postgres (Testcontainers)** | Full integration tests (rare) | ✅ Yes | ✅ Yes | Rarely used |

### Critical Gotcha: InMemory Provider Does NOT Enforce FK Constraints

The EF Core InMemory provider silently accepts foreign key violations. Any test that relies on cascade deletes or FK constraint violations being caught **MUST use SQLite in-memory**.

```csharp
// ✅ Correct — uses SQLite for FK enforcement
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
```

### Database Setup

- **Use `EnsureCreated()` not `Migrate()`** in test factories — it creates the schema from the current model without migration history.
- **Share the SQLite connection** across contexts created by the same factory. Keep the connection open for the test's lifetime; closing it destroys the in-memory database.

### Test DbContext Factories

Server tests use SQLite-backed factories in [`Veldrath.Server.Tests/Infrastructure/`](../../Veldrath.Server.Tests/Infrastructure/):

| Factory | Creates | Used By |
|---|---|---|
| [`TestDbContextFactory`](../../Veldrath.Server.Tests/Infrastructure/TestDbContextFactory.cs) | `ApplicationDbContext` (SQLite) | Zone, Auth, Character, GameHub tests |
| [`TestGameDbContextFactory`](../../Veldrath.Server.Tests/Infrastructure/TestGameDbContextFactory.cs) | `GameDbContext` (SQLite) | ServerSaveGameRepositoryTests, ServerHallOfFameRepositoryTests |

### DateTimeOffset / SQLite Converter Gotcha

SQLite cannot ORDER BY `DateTimeOffset` columns natively. The `ApplicationDbContext` and `EditorialDbContext` automatically apply a `DateTimeOffsetToStringConverter` when running under SQLite. This is handled in `OnModelCreating` — do not add manual conversion logic in tests.

### InMemory Repository Pattern

For tests that don't need a real database, use `InMemoryXxxRepository` implementations:

```csharp
public class InMemoryQuestRepository : IQuestRepository
{
    private readonly List<Quest> _quests;

    public InMemoryQuestRepository(List<Quest>? seed = null)
        => _quests = seed ?? [];

    public Task<List<Quest>> GetAllAsync()
        => Task.FromResult(_quests.ToList());

    public Task<Quest?> GetBySlugAsync(string slug)
        => Task.FromResult(_quests.FirstOrDefault(q => q.Slug == slug));
}
```

---

## MediatR / Handler Testing

### Two Approaches

1. **Integration test through `IMediator.Send()`** — Tests the handler + pipeline behaviors (validation, logging, performance). Use this when you want to verify the full pipeline.

2. **Unit test by instantiating the handler directly** — Call `Handle()` on the handler class. Use this when you want isolated testing of handler logic without pipeline overhead.

### Testing Handlers Directly

```csharp
[Fact]
public async Task AttackEnemyHandler_WithValidAttack_DealsDamage()
{
    // Arrange
    var combatService = new CombatService();
    var saveService = new FakeSaveGameService();
    var mediator = new Mock<IMediator>();  // For Publish() calls
    var handler = new AttackEnemyHandler(
        combatService, mediator.Object, saveService,
        new CombatSettings { GoldXPMultiplier = 1.0 },
        NullLogger<AttackEnemyHandler>.Instance);

    var player = CreateTestCharacter(health: 100);
    var enemy = CreateTestEnemy(health: 50, xp: 30, gold: 10);

    // Act
    var result = await handler.Handle(
        new AttackEnemyCommand { Player = player, Enemy = enemy },
        CancellationToken.None);

    // Assert
    result.Damage.Should().BeGreaterThan(0);
    enemy.Health.Should().BeLessThan(50);
}
```

### Validation Testing

**Always use a real FluentValidation validator** — never mock it. The `ValidationBehavior` passes the same `ValidationContext` to all validators when running via MediatR, which causes failure messages from one validator to appear in every other validator's result set. This is a known MediatR behavior.

```csharp
[Fact]
public async Task AttackEnemyValidator_WithDeadPlayer_ReturnsError()
{
    // Arrange
    var validator = new AttackEnemyValidator();
    var player = CreateTestCharacter(health: 0);

    // Act
    var result = await validator.ValidateAsync(
        new AttackEnemyCommand { Player = player, Enemy = CreateTestEnemy() });

    // Assert
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.ErrorMessage.Contains("alive"));
}
```

**For testing multi-validator failure aggregation:** Use **one validator with multiple failing rules**, not multiple separate validators. The shared `ValidationContext` makes multi-validator result aggregation unreliable.

### What to Test

- ✅ Success path — handler returns expected result
- ✅ Failure path — handler returns error result for invalid state
- ✅ Edge cases — zero values, empty collections, boundary conditions
- ✅ Validation — validator rejects invalid input
- ✅ Null inputs — handler handles null required fields gracefully

---

## Avalonia / UI Testing

Applies to: [`Veldrath.Client.Tests`](../../Veldrath.Client.Tests/), [`RealmForge.Tests`](../../RealmForge.Tests/).

### Thread Affinity (Critical)

Avalonia objects require the Avalonia dispatcher thread. Any test that creates `AvaloniaObject` derivatives (brushes, styles, view models that reference UI types) **MUST use `[AvaloniaFact]`** from `Avalonia.Headless.XUnit`.

```csharp
// ❌ WRONG — will throw "Call from invalid thread"
[Fact]
public void MapEdgeViewModel_CreatesStyle_Works() { ... }

// ✅ CORRECT — runs on Avalonia UI thread
[AvaloniaFact]
public void MapEdgeViewModel_CreatesStyle_Works() { ... }
```

- Tests that create `MapEdgeViewModel` (or `MapViewModel` with zone-exit edges) MUST use `[AvaloniaFact]`.
- Tests that create `MapViewModel` with NO zone connections (no `MapEdgeViewModel` created) can safely use `[Fact]`.

### `[Trait("Category", "UI")]`

Add `[Trait("Category", "UI")]` **only** when a real display is required. Headless tests do not need this trait.

### ViewModel Testing

Instantiate ViewModels directly and test ReactiveUI patterns:

```csharp
[AvaloniaFact]
public void CharacterViewModel_TakeDamage_UpdatesHealthProperty()
{
    var vm = new CharacterViewModel(new Character { Health = 100 });
    using var monitor = vm.WhenAnyValue(x => x.CurrentHealth).Subscribe(_ => { });

    vm.TakeDamage(30);

    vm.CurrentHealth.Should().Be(70);
}
```

ReactiveUI patterns to test:
- `RaiseAndSetIfChanged` — verify property change notifications
- `ReactiveCommand.CreateFromTask` — verify async command execution and `IsExecuting`
- `WhenAnyValue` — verify derived computed properties

### View Testing (Headless)

```csharp
[AvaloniaFact]
public void MainWindow_ContainsExpectedControls()
{
    var window = new MainWindow { DataContext = new MainViewModel() };
    window.Show();

    var button = window.FindControl<Button>("attackButton");
    button.Should().NotBeNull();
}
```

---

## Blazor / bunit Testing

Applies to: [`Veldrath.Web.Tests`](../../Veldrath.Web.Tests/), [`Veldrath.GameClient.Components.Tests`](../../Veldrath.GameClient.Components.Tests/), [`RealmFoundry.Tests`](../../RealmFoundry.Tests/).

**Use bunit v2.6.2.**

### Core API

| Operation | Method | Purpose |
|---|---|---|
| Render | `RenderComponent<T>()` | Render a component for testing |
| Find single | `Find("css-selector")` | Locate one element |
| Find many | `FindAll("css-selector")` | Locate multiple elements |
| Find component | `FindComponent<TChild>()` | Locate a child component by type |
| Markup assertion | `MarkupMatches(...)` | Assert HTML structure matches expected |
| Event callback | `callback.AssertNotCalled()` / `callback.AssertCalledOnce()` | Verify event callbacks |

### MudBlazor Service Registration

Components that depend on MudBlazor must have `MudServices` registered in bunit's `TestContext`:

```csharp
using var ctx = new TestContext();
ctx.Services.AddMudServices();

// Now render the component — MudBlazor dependencies are available
var cut = ctx.RenderComponent<MyMudComponent>();
```

### Component Parameter Testing

```csharp
var cut = ctx.RenderComponent<MyComponent>(parameters => parameters
    .Add(p => p.Title, "Test Title")
    .Add(p => p.IsEnabled, true)
    .Add(p => p.OnClick, () => clicked = true));
```

### EventCallback Testing

```csharp
var callback = ctx.RenderComponent<ParentComponent>(parameters => parameters
    .Add(p => p.OnSubmit, args => receivedArgs = args));

var child = callback.FindComponent<ChildForm>();
child.Find("button[type='submit']").Click();

receivedArgs.Should().NotBeNull();
```

### What NOT to Test

Do not test MudBlazor's internal behavior — only test your component's logic, parameter bindings, and event handling. The framework's rendering, theming, and interaction behavior is MudBlazor's responsibility.

### Detailed Reference

For comprehensive bunit patterns, parameter testing, and MudBlazor-specific configuration, see [`.github/instructions/blazor-component-development.md`](blazor-component-development.md#testing).

---

## Server / Integration Testing

Applies to: [`Veldrath.Server.Tests`](../../Veldrath.Server.Tests/).

### WebApplicationFactory

Use `WebApplicationFactory<Program>` for hosted integration tests that exercise the full pipeline:

```
HTTP request → Controller or Hub → MediatR.Send(command) → Handler → Response
```

### Database

Always use **SQLite in-memory** (not EF Core InMemory) for server tests. SQL semantics (FK constraints, unique indexes, transaction behavior) are critical for server-side integration scenarios.

### Hub Testing

Hub tests live in [`Veldrath.Server.Tests/Features/`](../../Veldrath.Server.Tests/Features/):

| File | Purpose |
|---|---|
| [`GameHubTests.cs`](../../Veldrath.Server.Tests/Features/GameHubTests.cs) | Main hub integration tests |
| [`GameHubChatCommandTests.cs`](../../Veldrath.Server.Tests/Features/GameHubChatCommandTests.cs) | Chat-specific hub tests |
| [`GameHubRegionTests.cs`](../../Veldrath.Server.Tests/Features/GameHubRegionTests.cs) | Region movement tests |

Use **Fake** implementations of SignalR primitives:

| Fake | Implements | Purpose |
|---|---|---|
| `FakeClientProxy` | `IClientProxy` | Captures messages sent to client/group for assertion |
| `FakeHubCallerClients` | `IHubCallerClients` | Provides `FakeClientProxy` instances for Caller, Group, OthersInGroup |
| `FakeGroupManager` | `IGroupManager` | Captures group add/remove operations |

### What to Test in Hubs

| ✅ Test This | ❌ Don't Test This |
|---|---|
| Hub method calls `mediator.Send` with the correct command | The actual game logic inside the handler (tested in Core) |
| Hub returns the correct response shape for error/success cases | SignalR transport/infrastructure |
| Broadcasts are sent to the correct groups | That MediatR works (it's framework code) |
| Connection guards reject unauthenticated/uncharactered calls | JWT validation (framework code) |
| Character ownership is enforced | Database queries (test repos directly) |

For detailed hub test patterns, see [`.github/instructions/hub-development.md`](hub-development.md#testing).

---

## Code Coverage

### Collection

Coverage is collected via **coverlet.collector** (v10.0.1) with configuration in [`coverage.runsettings`](../../coverage.runsettings).

```powershell
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage"
```

### Exclusions

Apply `[ExcludeFromCodeCoverage]` to:

- **Entry points** — `Program` classes (minimal API / ASP.NET Core startup)
- **App bootstrappers** — `App` classes (Avalonia, Blazor)
- **Compiled XAML resources** — `.axaml` / `.xaml` code-behind files
- **Thin infrastructure wrappers** — `HubConnectionFactory`, `HubConnectionWrapper` in [`Veldrath.Client`](../../Veldrath.Client/)

### Test Project Assemblies

Every test project has `Properties/AssemblyInfo.cs` with:

```csharp
[assembly: ExcludeFromCodeCoverage]
```

This ensures the test code itself never contributes to coverage metrics.

---

## Test Project Setup

### Required Package References

Every test project references:

| Package | Version | Purpose |
|---|---|---|
| `xunit` | 2.9.3 | Test framework |
| `FluentAssertions` | 8.10.0 | Assertion library |
| `coverlet.collector` | 10.0.1 | Code coverage collection |

Additional project-specific references (bunit, Avalonia.Headless.XUnit, etc.) are added per testing surface.

### Required Files

Every test project has:
- `Properties/AssemblyInfo.cs` with `[assembly: ExcludeFromCodeCoverage]`
- Reference to the corresponding source project (`<ProjectReference>`)

---

## Known Gotchas

These are cross-referenced from [`.github/agent-memory/engine-codebase.md`](../agent-memory/engine-codebase.md). Consult that file for full details.

### Castle.DynamicProxy / Moq Constraints

Types used as generic type arguments in `Mock<IFoo<T>>` must be `public`. `file` or `internal` test types used as generic args will cause a Castle proxy error at runtime.

`EnemyGenerator(IEnemyRepository, ILogger<EnemyGenerator>)` has no parameterless constructor — cannot use `Mock.Of<EnemyGenerator>()`. Construct a real instance instead.

### MediatR / IPipelineBehavior with Mocked Validators

ValidationBehavior runs validators via `Task.WhenAll` but passes the **same** `ValidationContext` to all of them. Failure messages from one validator appear in every other validator's result set. Test multi-validator failure aggregation with one validator containing multiple rules — not multiple validators.

### Model `required` Members

`Location` has four `required` members: `Id`, `Name`, `Description`, `Type`. `DungeonRoom` requires `Id` and `Type`. `DungeonInstance` requires `Id`, `LocationId`, and `Name`. All `required` members must be set in object initializers or tests will not compile (CS9035).

### Random.Shared in Handlers

`ExploreLocationCommandHandler` uses `Random.Shared.Next(100)` — non-deterministic. Seed or mock randomness for reproducible tests. If a handler uses `Random.Shared` directly, the test must account for non-determinism or inject a seed.

### Static State in Services

`EnterDungeonHandler._activeDungeons` is `private static Dictionary<string, DungeonInstance>`. Static state in handlers persists across tests — use a file-scoped `ActiveDungeonScope` helper that injects and cleans up the entry in `Dispose`.

### Enum Ambiguity

`RarityTier` enum is in `RealmEngine.Shared.Models`. Separate from `ItemRarity` (used on items). Do not confuse the two in test assertions.

### PowerDto.School Is Raw

`EfCorePowerRepository` returns `Power.School` directly (e.g. `"fire"`, `"arcane"`). No tradition mapping is applied. Tests asserting on `PowerDto.School` should check raw values.

### Avalonia Thread Affinity

`MapEdgeViewModel.ComputeStyle` creates `SolidColorBrush` (inherits `AvaloniaObject`) which requires the Avalonia dispatcher thread. Any test that constructs `MapEdgeViewModel` must use `[AvaloniaFact]`.

---

## Testing Checklist

When writing a new test, verify:

- [ ] **Success case** — Does the test verify the happy path?
- [ ] **Failure case** — Does the test verify error handling?
- [ ] **Edge case** — Empty input? Zero values? Boundary conditions?
- [ ] **Validation** — Does the validator reject invalid input?
- [ ] **Null inputs** — Does the handler handle null required fields?
- [ ] **Non-determinism** — Is `Random.Shared` usage accounted for?
- [ ] **Fixture isolation** — Could this test interfere with other tests via static state?

---

## See Also

- [`.github/instructions/hub-development.md`](hub-development.md) — Hub integration test patterns, Fake SignalR infrastructure
- [`.github/instructions/blazor-component-development.md`](blazor-component-development.md#testing) — Detailed bunit component testing patterns
- [`.github/instructions/ef-core-patterns.md`](ef-core-patterns.md#testing-with-ef-core) — EF Core test provider selection and gotchas
- [`.github/instructions/engine-features.md`](engine-features.md) — How to test new MediatR handlers
- [`.github/agent-memory/engine-codebase.md`](../agent-memory/engine-codebase.md) — Testing gotchas, model facts, handler quirks
- [`coverage.runsettings`](../../coverage.runsettings) — Code coverage configuration
- [`.roo/skills/run-all-tests/`](../../.roo/skills/run-all-tests/) — Skill to run the full test suite
- [`.roo/skills/run-tests-by-component/`](../../.roo/skills/run-tests-by-component/) — Skill to run a single component's tests
