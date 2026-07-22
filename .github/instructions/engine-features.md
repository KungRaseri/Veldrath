# Engine Feature Development

Scope: [`RealmEngine.Core`](../../RealmEngine.Core/) — the framework-agnostic game logic library. This is the most common code-writing task: creating new MediatR commands, queries, handlers, validators, and response DTOs.

Engine libraries (`Core`, `Shared`, `Data`) have **zero dependency on any UI framework** — this is a hard rule. Every game operation is a MediatR `IRequest<TResponse>` that any .NET consumer can call via `mediator.Send(command)`.

---

## Feature Organization — Vertical Slice

### Directory Structure

Features live in [`RealmEngine.Core/Features/{FeatureName}/`](../../RealmEngine.Core/Features/). Each feature folder organizes its files by operation type:

```
RealmEngine.Core/Features/
├── Combat/
│   ├── Commands/
│   │   ├── AttackEnemy/
│   │   │   ├── AttackEnemyCommand.cs      # Command record + response DTO
│   │   │   ├── AttackEnemyHandler.cs      # Handler class
│   │   │   └── AttackEnemyValidator.cs    # FluentValidation validator
│   │   ├── DefendAction/
│   │   │   ├── DefendActionCommand.cs
│   │   │   ├── DefendActionHandler.cs
│   │   │   └── DefendActionValidator.cs
│   │   ├── FleeFromCombat/
│   │   └── EncounterBoss/
│   ├── Queries/
│   │   ├── GetCombatState/
│   │   │   ├── GetCombatStateQuery.cs     # Query record + response DTO
│   │   │   └── GetCombatStateHandler.cs   # Handler class
│   │   └── GetEnemyInfo/
│   └── Services/
│       ├── CombatService.cs
│       └── EnemyAbilityAIService.cs
├── CharacterCreation/
│   ├── Commands/                          # Sometimes flat — no subdirectory per command
│   │   ├── CreateCharacterCommand.cs
│   │   └── CreateCharacterHandler.cs
│   ├── Queries/
│   └── Services/
├── Inventory/
├── Shop/
├── Crafting/
└── ... (40+ feature folders)
```

### Naming Conventions

| Pattern | Example | Purpose |
|---|---|---|
| `{Action}{Entity}Command` | `CreateCharacterCommand`, `LevelUpCharacterCommand`, `GrantAchievementCommand` | Mutates game state |
| `{Entity}Query` | `GetInventoryQuery`, `GetCombatStateQuery` | Reads game state |
| `{Action}{Entity}Result` | `AttackEnemyResult`, `FleeFromCombatResult` | Response DTO for commands |
| `{Entity}Dto` | `CombatStateDto`, `CharacterDto` | Response DTO for queries |

### When to Subdirectory vs Flat

- **Subdirectory per operation** (e.g., `Commands/AttackEnemy/`) — Use when the command has a validator AND a handler. The three files (command, handler, validator) live together.
- **Flat** (e.g., `Commands/CreateCharacterCommand.cs` + `Commands/CreateCharacterHandler.cs`) — Use when the handler is large enough to warrant its own file without a validator, or when validators don't exist for the operation.

---

## Command/Query Record Patterns

### Commands (Mutate State)

Commands are `record` types implementing `IRequest<TResponse>`. Use `init` properties for complex DTOs, positional constructors for simple ones.

```csharp
/// <summary>
/// Command to execute a player attack against an enemy.
/// </summary>
public record AttackEnemyCommand : IRequest<AttackEnemyResult>
{
    /// <summary>
    /// Gets the player character performing the attack.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the enemy being attacked.
    /// </summary>
    public required Enemy Enemy { get; init; }

    /// <summary>
    /// Gets the combat log for recording combat events.
    /// </summary>
    public CombatLog? CombatLog { get; init; }
}
```

### Queries (Read State)

Queries follow the same pattern but MUST NOT modify any state:

```csharp
/// <summary>
/// Query to get the current state of combat.
/// </summary>
public record GetCombatStateQuery : IRequest<CombatStateDto>
{
    /// <summary>
    /// Gets the player character in combat.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the enemy in combat.
    /// </summary>
    public required Enemy Enemy { get; init; }
}
```

### Response DTOs

**Always define a separate response DTO.** Never return entity models directly from handlers. Response DTOs should be simple records with only the data the caller needs.

```csharp
/// <summary>
/// Result of an attack command.
/// </summary>
public record AttackEnemyResult
{
    /// <summary>
    /// Gets the amount of damage dealt.
    /// </summary>
    public int Damage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the attack was a critical hit.
    /// </summary>
    public bool IsCritical { get; init; }

    /// <summary>
    /// Gets a value indicating whether the enemy was defeated.
    /// </summary>
    public bool IsEnemyDefeated { get; init; }

    /// <summary>
    /// Gets the experience gained from defeating the enemy.
    /// </summary>
    public int ExperienceGained { get; init; }

    /// <summary>
    /// Gets the gold gained from defeating the enemy.
    /// </summary>
    public int GoldGained { get; init; }
}
```

For operations that can fail, include success/error indicators:

```csharp
/// <summary>
/// Result of a flee attempt.
/// </summary>
public record FleeFromCombatResult
{
    /// <summary>
    /// Gets a value indicating whether the flee attempt was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the flee attempt result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
```

### Positional vs `init` Properties

| Style | When to Use | Example |
|---|---|---|
| Positional record | Simple DTOs with ≤3 required fields | `record CreateItemCommand(string Name, string Description) : IRequest<CreateItemResult>` |
| `init` properties | Complex DTOs with optional/nullable fields or many properties | `record AttackEnemyCommand { public required Character Player { get; init; } ... }` |

---

## Handler Structure

### Handler Class

The handler lives in the same file as the command/query record (vertical slice). It implements `IRequestHandler<TRequest, TResponse>` and uses constructor injection for dependencies.

```csharp
/// <summary>
/// Handles the AttackEnemy command.
/// </summary>
public class AttackEnemyHandler : IRequestHandler<AttackEnemyCommand, AttackEnemyResult>
{
    private readonly CombatService _combatService;
    private readonly IMediator _mediator;
    private readonly ISaveGameService _saveGameService;
    private readonly ICombatSettings _combatSettings;
    private readonly ILogger<AttackEnemyHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttackEnemyHandler"/> class.
    /// </summary>
    /// <param name="combatService">The combat service.</param>
    /// <param name="mediator">The mediator for publishing events.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="combatSettings">The combat difficulty settings.</param>
    /// <param name="logger">The logger.</param>
    public AttackEnemyHandler(
        CombatService combatService,
        IMediator mediator,
        ISaveGameService saveGameService,
        ICombatSettings combatSettings,
        ILogger<AttackEnemyHandler> logger)
    {
        _combatService = combatService;
        _mediator = mediator;
        _saveGameService = saveGameService;
        _combatSettings = combatSettings;
        _logger = logger;
    }

    /// <summary>
    /// Handles the attack enemy command and returns the result of the attack.
    /// </summary>
    /// <param name="request">The attack enemy command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the attack result.</returns>
    public async Task<AttackEnemyResult> Handle(
        AttackEnemyCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Extract data from request
        var player = request.Player;
        var enemy = request.Enemy;

        // 2. Execute game logic (delegate to domain services)
        var combatResult = await _combatService.ExecutePlayerAttack(player, enemy);

        // 3. Mutate state
        enemy.Health = Math.Max(0, enemy.Health - combatResult.Damage);

        // 4. Publish domain events
        await _mediator.Publish(new AttackPerformed(player.Name, enemy.Name, combatResult.Damage),
            cancellationToken);

        // 5. Handle side effects (defeat, rewards, quests, achievements)
        if (enemy.Health <= 0)
        {
            player.Experience += enemy.XP;
            player.Gold += enemy.GoldReward;
            await _mediator.Publish(new EnemyDefeated(player.Name, enemy.Name), cancellationToken);
        }

        // 6. Log
        _logger.LogInformation("Player {PlayerName} attacked {EnemyName} for {Damage} damage",
            player.Name, enemy.Name, combatResult.Damage);

        // 7. Return typed response DTO
        return new AttackEnemyResult
        {
            Damage = combatResult.Damage,
            IsCritical = combatResult.IsCritical,
            IsEnemyDefeated = enemy.Health <= 0,
            ExperienceGained = enemy.Health <= 0 ? enemy.XP : 0,
            GoldGained = enemy.Health <= 0 ? enemy.GoldReward : 0,
        };
    }
}
```

### The `Handle` Method Pattern

Every `Handle` method should follow this sequence:

1. **Extract** data from the request record
2. **Execute** game logic (delegate to domain services for complex calculations)
3. **Mutate** state (apply results to entities)
4. **Publish** domain events via `_mediator.Publish()` for side effects
5. **Persist** changes via repositories or `ISaveGameService` (when not using pipeline transaction)
6. **Log** at `Information` level for significant operations
7. **Return** a typed response DTO

### Handler Size

Keep handlers focused. If a handler exceeds ~100 lines, extract reusable logic into a domain service in the feature's `Services/` directory or in [`RealmEngine.Core/Services/`](../../RealmEngine.Core/Services/).

---

## Validation

### FluentValidation Validator

Validators implement `AbstractValidator<TCommand>` and live in the same directory as the command:

```csharp
/// <summary>
/// Validates the AttackEnemy command.
/// </summary>
public class AttackEnemyValidator : AbstractValidator<AttackEnemyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttackEnemyValidator"/> class.
    /// </summary>
    public AttackEnemyValidator()
    {
        RuleFor(x => x.Player)
            .NotNull().WithMessage("Player cannot be null");

        RuleFor(x => x.Player.Health)
            .GreaterThan(0).WithMessage("Player must be alive to attack")
            .When(x => x.Player != null);

        RuleFor(x => x.Enemy)
            .NotNull().WithMessage("Enemy cannot be null");

        RuleFor(x => x.Enemy.Health)
            .GreaterThan(0).WithMessage("Enemy must be alive to be attacked")
            .When(x => x.Enemy != null);
    }
}
```

### Validation Rules Checklist

When writing a validator, check:

- [ ] **Required fields** — `.NotNull()`, `.NotEmpty()`
- [ ] **Range constraints** — `.GreaterThan()`, `.LessThan()`, `.InclusiveBetween()`
- [ ] **Business rule preconditions** — "player must be alive", "item must be in inventory"
- [ ] **Conditional rules** — `.When(x => x.SomeCondition)` for dependent validation
- [ ] **Meaningful error messages** — `.WithMessage("specific, actionable error")`

### Async Validation

Use `MustAsync` for validation that requires data lookup:

```csharp
RuleFor(x => x.CharacterId)
    .MustAsync(async (id, ct) => await _characterRepo.ExistsAsync(id, ct))
    .WithMessage("Character does not exist");
```

Note: `MustAsync` requires the validator to have injected dependencies (add a constructor that takes the repository).

### Pipeline Integration

Validators are automatically invoked by the `ValidationBehavior` pipeline behavior — handlers do not need to call `ValidateAsync()` themselves. However, handlers may still perform **defensive validation** for invariants that the validator cannot express (e.g., complex business rules that depend on loaded data).

---

## Error Handling

### Business Logic Errors

Handlers should **NOT throw exceptions for business logic errors.** Return result objects with success/failure indicators:

```csharp
public record SomeResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the result data (only valid when <see cref="Success"/> is <see langword="true"/>).</summary>
    public SomeDto? Data { get; init; }
}
```

### Validation Errors

Let FluentValidation handle validation errors — the `ValidationBehavior` throws `ValidationException` which the caller (hub, controller) catches and converts to an appropriate error response. Do not catch validation exceptions inside handlers.

### When to Throw

Exceptions are for **truly exceptional cases** only:
- Infrastructure failures (database unavailable, network timeout)
- Programming errors (null reference where invariant guarantees non-null)
- Corrupted state that the handler cannot recover from

---

## Service Registration

### MediatR Auto-Discovery

Handlers are auto-discovered via assembly scanning in [`ServiceCollectionExtensions.AddRealmEngineMediatR()`](../../RealmEngine.Core/ServiceCollectionExtensions.cs):

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
});
```

No manual handler registration is needed — adding a new handler in `RealmEngine.Core/Features/` is sufficient.

### The `UseExternal()` Pattern

When a Core handler depends on a repository interface defined in [`RealmEngine.Shared/Abstractions/`](../../RealmEngine.Shared/Abstractions/) whose implementation lives in [`RealmEngine.Data/`](../../RealmEngine.Data/), the consuming application (e.g., [`Veldrath.Server`](../../Veldrath.Server/)) must register the implementation:

```csharp
// In Veldrath.Server/Program.cs
builder.Services.AddRealmEngineCore(p => p.UseExternal());

// Then manually register every repository the server needs:
builder.Services.AddScoped<IQuestRepository, EfCoreQuestRepository>();
builder.Services.AddScoped<IEnemyRepository, EfCoreEnemyRepository>();
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
// ... etc.
```

If a new handler injects an interface not already registered, the server will crash at startup with:
```
Unable to resolve service for type 'IFoo' while attempting to activate 'SomeHandler'.
```

**Build-Check Pattern:** After adding a new handler that depends on a new repository, run `dotnet build Veldrath.Server` to catch missing registrations at compile/build time.

### Custom Services

Non-MediatR services (domain services, generators, calculators) are registered in [`ServiceCollectionExtensions.AddRealmEngineCore()`](../../RealmEngine.Core/ServiceCollectionExtensions.cs):

```csharp
// Domain services — scoped
services.AddScoped<LootTableService>();
services.AddScoped<ShopEconomyService>();

// Generators — scoped
services.AddScoped<ItemGenerator>();
services.AddScoped<EnemyGenerator>();

// Interfaces to implementations
services.AddScoped<ISaveGameService, SaveGameService>();
```

Add new service registrations to the appropriate section of `AddRealmEngineCore()`.

---

## Pipeline Behaviors

Three behaviors execute around every handler invocation, in order:

```
Client calls IMediator.Send(command)
  → 1. LoggingBehavior   — Serilog structured logging of request/response
  → 2. ValidationBehavior — FluentValidation (throws ValidationException on failure)
  → 3. PerformanceBehavior — logs warnings for slow handlers
  → Handler.Handle(request, ct)
  → Response returned to caller
```

### Key Facts

- Behaviors are **transparent** — handlers don't need to know about them.
- If a handler isn't being called, **check if validation is failing first**. The `ValidationBehavior` short-circuits the pipeline before the handler runs.
- The `PerformanceBehavior` logs a warning if any handler takes longer than a threshold (default: 500 ms).
- In tests, you can test handlers directly (bypassing behaviors) or through `IMediator.Send()` (including behaviors).

---

## When to Use What

### Command vs Query

| | Command | Query |
|---|---|---|
| **Mutates state?** | Yes | No |
| **Implements** | `IRequest<TResult>` | `IRequest<TResult>` |
| **Naming** | `{Action}{Entity}Command` | `{Entity}Query` or `Get{Entity}Query` |
| **Response** | Result DTO with success/error | DTO with requested data |
| **Validator?** | Usually yes | Sometimes (for parameter validation) |

**Rule of thumb:** If the operation modifies the game world (characters, inventory, combat state, etc.), it's a Command. If it only reads data, it's a Query.

### Handler vs Domain Service

| Put logic in a Handler when... | Extract to a Domain Service when... |
|---|---|
| The logic is specific to this one operation | The logic is reused across multiple handlers |
| The handler is under ~100 lines | The handler exceeds ~100 lines |
| The logic is orchestration (load → compute → save) | The logic is pure computation (damage formulas, AI decisions, procedural generation) |

Domain services live in:
- Feature-specific: `RealmEngine.Core/Features/{FeatureName}/Services/`
- Cross-cutting: [`RealmEngine.Core/Services/`](../../RealmEngine.Core/Services/)

### Handler vs Generator

| Handler | Generator |
|---|---|
| Processes a specific command/query | Creates procedural content |
| Returns a typed response DTO | Returns generated entities (enemies, items, NPCs) |
| Example: `AttackEnemyHandler` | Example: `EnemyGenerator`, `ItemGenerator` |

Generators live in [`RealmEngine.Core/Generators/`](../../RealmEngine.Core/Generators/).

### New Feature File vs Add to Existing

| Scenario | Action |
|---|---|
| New operation on an existing entity (e.g., `SellItemCommand` for Inventory) | Add to existing feature folder: `Inventory/Commands/SellItem/` |
| New entity/domain (e.g., a new `Guild` system) | Create a new feature folder: `Features/Guild/` |

---

## Prohibited Patterns

These actions MUST NEVER be taken in engine code:

| ❌ Anti-Pattern | ✅ Correct Approach |
|---|---|
| Returning entity models directly from handlers | Always return a response DTO |
| Throwing exceptions for validation failures | Let FluentValidation handle it via pipeline behavior |
| Business logic in controllers or hubs | Always go through MediatR → handler |
| Circular dependencies between handlers (A → B → A) | Extract shared logic to a domain service |
| Direct DB access in handlers | Always go through repository interfaces |
| Handler calling another handler via `_mediator.Send()` | Use `_mediator.Publish()` for events, or extract shared logic to a service |
| Taking a dependency on a UI framework (Avalonia, ASP.NET Core, Blazor) | Engine libraries have zero UI dependencies — this is a hard rule |
| Using `Random.Shared` directly without injection | Inject an `IRandomProvider` or seed for reproducibility |
| Static state in handlers (unless explicitly designed for cross-connection tracking) | Use injected scoped services |

---

## Quick Checklist — New Feature

When creating a new feature, verify:

- [ ] **Command/Query record** — Implements `IRequest<TResponse>`, all required properties documented
- [ ] **Handler class** — Implements `IRequestHandler<TRequest, TResponse>`, constructor injection, follows Handle method pattern
- [ ] **Validator** — Implements `AbstractValidator<TCommand>`, checks required fields, range constraints, business rules
- [ ] **Response DTO** — Separate record with only the data the caller needs, success/error indicators if applicable
- [ ] **XML docs** — `<summary>` on every public type and member (CS1591 is a compile-time error)
- [ ] **No UI dependencies** — Handler only references `RealmEngine.Shared` and `RealmEngine.Core`
- [ ] **Repository interfaces** — New repository interfaces in `RealmEngine.Shared/Abstractions/`, implementations in `RealmEngine.Data/Repositories/`
- [ ] **DI registration** — New repository implementations registered in `Program.cs` (server) or `ServiceCollectionExtensions.cs` (core)
- [ ] **Tests** — Success path, failure path, edge cases, validation, null inputs

---

## See Also

- [`.github/instructions/testing.md`](testing.md) — How to test handlers, validators, and pipelines
- [`.github/instructions/ef-core-patterns.md`](ef-core-patterns.md) — Repository patterns, DbContext separation, `UseExternal()` registration
- [`.github/instructions/xml-documentation.md`](xml-documentation.md) — CS1591 enforcement and doc comment templates
- [`.github/instructions/hub-development.md`](hub-development.md) — How hubs call MediatR handlers (Hub→MediatR bridge pattern)
- [`RealmEngine.Core/ServiceCollectionExtensions.cs`](../../RealmEngine.Core/ServiceCollectionExtensions.cs) — DI registration for services, MediatR, and validators
- [`RealmEngine.Core/Behaviors/`](../../RealmEngine.Core/Behaviors/) — Pipeline behavior implementations (Logging, Validation, Performance)
- [`RealmEngine.Core/Features/`](../../RealmEngine.Core/Features/) — All 40+ feature folders with real-world examples
- [`.roo/skills/create-new-feature/`](../../.roo/skills/create-new-feature/) — Skill to scaffold a new MediatR vertical-slice feature
