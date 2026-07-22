# XML Documentation (CS1591)

Scope: **ALL non-test projects** in the solution. This applies to [`RealmEngine.Core`](../../RealmEngine.Core/), [`RealmEngine.Shared`](../../RealmEngine.Shared/), [`RealmEngine.Data`](../../RealmEngine.Data/), [`Veldrath.Server`](../../Veldrath.Server/), [`Veldrath.Client`](../../Veldrath.Client/), [`Veldrath.Web`](../../Veldrath.Web/), [`Veldrath.GameClient.Core`](../../Veldrath.GameClient.Core/), [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/), [`Veldrath.Auth`](../../Veldrath.Auth/), [`Veldrath.Auth.Blazor`](../../Veldrath.Auth.Blazor/), [`Veldrath.Contracts`](../../Veldrath.Contracts/), [`Veldrath.Discord`](../../Veldrath.Discord/), [`Veldrath.Assets`](../../Veldrath.Assets/), [`RealmForge`](../../RealmForge/), [`RealmFoundry`](../../RealmFoundry/), and [`RealmUI.Fonts`](../../RealmUI.Fonts/).

**Does NOT apply to** test projects (any project with `<IsTestProject>true</IsTestProject>` in its `.csproj`).

---

## The Rule

**Every publicly visible type and member must have an XML doc comment (`<summary>` at minimum). CS1591 is a compile-time ERROR, not a warning.**

There are NO exceptions and NO workarounds. If the build fails with CS1591, fix the missing doc comment — do not suppress the error.

---

## Enforcement Mechanism

Enforcement lives in [`Directory.Build.targets`](../../Directory.Build.targets) and applies to every non-test project automatically:

```xml
<PropertyGroup Condition="'$(IsTestProject)' != 'true'">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarningsAsErrors>CS1591</WarningsAsErrors>
</PropertyGroup>
```

This means:
- `GenerateDocumentationFile>true` — every project emits an XML doc file, which means the compiler checks all public members.
- `WarningsAsErrors>CS1591` — any missing doc comment is promoted from warning to error.
- The `'$(IsTestProject)' != 'true'` condition means test projects are exempt (they set `<IsTestProject>true</IsTestProject>` in their `.csproj`).

---

## Doc Comment Templates

### Classes

```csharp
/// <summary>Brief description of what this class does and its role in the system.</summary>
public class MyService
{
}
```

### Records (Positional)

Positional records require `<summary>` on the record type AND `<param>` on each constructor parameter:

```csharp
/// <summary>
/// Represents a request to craft a specific item.
/// </summary>
/// <param name="CharacterId">The character initiating the craft.</param>
/// <param name="RecipeSlug">The slug of the recipe to craft (e.g. <c>"iron-sword"</c>).</param>
public record CraftItemHubCommand(Guid CharacterId, string RecipeSlug) : IRequest<CraftItemHubResult>;
```

### Records (Regular / Non-Positional)

```csharp
/// <summary>Result returned by <see cref="CraftItemHubCommandHandler"/>.</summary>
public record CraftItemHubResult
{
    /// <summary>Gets a value indicating whether the craft succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }
}
```

### Record Structs

Same rules as records:

```csharp
/// <summary>
/// Immutable snapshot of a character's tile position at a point in time.
/// </summary>
/// <param name="X">The tile X coordinate.</param>
/// <param name="Y">The tile Y coordinate.</param>
public readonly record struct TilePosition(int X, int Y);
```

### Interfaces

Document the interface AND every member:

```csharp
/// <summary>Repository interface for reading quest catalog data.</summary>
public interface IQuestRepository
{
    /// <summary>Returns all active quests.</summary>
    Task<List<Quest>> GetAllAsync();

    /// <summary>Returns a single quest by slug.</summary>
    /// <param name="slug">The unique quest identifier.</param>
    /// <returns>The matching quest, or <see langword="null"/> if not found.</returns>
    Task<Quest?> GetBySlugAsync(string slug);
}
```

### Methods

Every method requires `<summary>`, `<param>` for each parameter, and `<returns>` (unless `void`):

```csharp
/// <summary>
/// Loads the specified character, applies the damage amount,
/// and persists the updated attributes blob.
/// </summary>
/// <param name="request">The damage request containing character ID, amount, and source.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A <see cref="TakeDamageHubResult"/> describing the outcome, including current HP.</returns>
public async Task<TakeDamageHubResult> Handle(
    TakeDamageHubCommand request,
    CancellationToken cancellationToken)
```

For `void` methods, omit `<returns>`:

```csharp
/// <summary>Releases all resources held by this instance.</summary>
public void Dispose()
```

### Properties

```csharp
/// <summary>Gets or sets the display name shown in the UI.</summary>
public string DisplayName { get; set; } = string.Empty;

/// <summary>Gets a value indicating whether the entity is currently active in the game world.</summary>
public bool IsActive { get; init; }
```

### Constructors (Primary and Body)

At minimum, use the standard constructor summary:

```csharp
/// <summary>Initializes a new instance of <see cref="MyService"/>.</summary>
/// <param name="repository">The repository used to load data.</param>
/// <param name="logger">The logger instance.</param>
public MyService(IMyRepository repository, ILogger<MyService> logger)
{
}
```

For primary constructors on classes:

```csharp
/// <summary>Initializes a new instance of <see cref="EfCoreQuestRepository"/>.</summary>
/// <param name="db">The content database context.</param>
/// <param name="logger">The logger instance.</param>
public class EfCoreQuestRepository(ContentDbContext db, ILogger<EfCoreQuestRepository> logger)
    : IQuestRepository
```

### Static Fields / Constants

```csharp
/// <summary>The default mana cost deducted per ability use.</summary>
public const int DefaultManaCost = 10;

/// <summary>XP required to advance one skill rank.</summary>
public const int XpPerRank = 100;
```

### Enums

Document the enum type AND every member:

```csharp
/// <summary>Describes the type of zone — used for spawning rules and UI presentation.</summary>
public enum ZoneType
{
    /// <summary>A safe town zone with no hostile spawns.</summary>
    Town,

    /// <summary>A wilderness zone with procedurally spawned enemies.</summary>
    Wilderness,

    /// <summary>An instanced dungeon with preset encounters.</summary>
    Dungeon,
}
```

### Delegates

```csharp
/// <summary>
/// Represents a callback invoked when the hub connection state changes.
/// </summary>
/// <param name="sender">The connection service that raised the event.</param>
/// <param name="state">The new connection state.</param>
public delegate void ConnectionStateChangedHandler(object sender, ConnectionState state);
```

---

## Special Cases

### `<inheritdoc/>`

Use `<inheritdoc/>` when overriding a member whose base documentation is sufficient. The compiler copies the base doc up:

```csharp
/// <inheritdoc/>
protected override async Task OnInitializedAsync()
{
    await base.OnInitializedAsync();
}

/// <inheritdoc/>
public void Dispose()
{
    _connection.Dispose();
}
```

**When NOT to use `<inheritdoc/>`:** If the override adds meaningful behavior beyond what the base describes, write a full doc comment instead. `<inheritdoc/>` is for thin passthrough overrides only.

### `<exception cref="...">`

Use when a method explicitly throws:

```csharp
/// <summary>Retrieves a zone by its ID. Throws if the zone does not exist.</summary>
/// <param name="zoneId">The zone identifier.</param>
/// <returns>The matching zone entity.</returns>
/// <exception cref="InvalidOperationException">Thrown when the zone ID is not found.</exception>
public Zone GetRequiredZone(string zoneId)
```

### `<typeparam name="T">`

Required for generic type parameters:

```csharp
/// <summary>A generic repository for read-only catalog access.</summary>
/// <typeparam name="T">The entity type managed by this repository.</typeparam>
public interface IReadOnlyRepository<T> where T : class
{
    /// <summary>Returns all entities of type <typeparamref name="T"/>.</summary>
    Task<List<T>> GetAllAsync();
}
```

### `<see cref="..."/>`

For cross-references within documentation:

```csharp
/// <summary>
/// Handles <see cref="CraftItemHubCommand"/> by validating the recipe
/// and deducting crafting costs via <see cref="ICharacterRepository"/>.
/// </summary>
public class CraftItemHubCommandHandler : IRequestHandler<CraftItemHubCommand, CraftItemHubResult>
```

### `<c>` and `<code>`

- `<c>` for inline code references within a sentence.
- `<code>` for multi-line code blocks.

```csharp
/// <summary>
/// Parses the command string. Prefixing with <c>/</c> triggers command parsing;
/// everything else is treated as a plain chat message.
/// </summary>
```

### `<para>`

For multi-paragraph comments:

```csharp
/// <summary>
/// Validates the movement request against the tilemap collision layer.
/// </summary>
/// <para>
/// The validation enforces: 1-tile distance limit, collision-mask blocking,
/// and a 100 ms cooldown between moves from the same character.
/// </para>
```

### `<paramref name="..."/>` and `<typeparamref name="..."/>`

Reference parameter/type names within doc text:

```csharp
/// <summary>
/// Applies all content entity configurations to the given <paramref name="builder"/>.
/// </summary>
/// <param name="builder">The model builder to configure.</param>
```

---

## Prohibited Patterns

These actions MUST NEVER be taken under any circumstance:

| Forbidden Action | Why It's Wrong |
|---|---|
| Adding `<NoWarn>CS1591</NoWarn>` to any `.csproj` file | Suppresses the error for the entire project, hiding real gaps |
| Adding CS1591 to `<NoWarn>` in [`Directory.Build.props`](../../Directory.Build.props) or [`Directory.Build.targets`](../../Directory.Build.targets) | Disables enforcement globally — catastrophic |
| Using `[assembly: SuppressMessage(...)]` for CS1591 | Same effect as NoWarn — hides the error |
| Commenting out `<GenerateDocumentationFile>` or setting it to `false` | Prevents XML doc generation entirely |
| Adding empty `<summary/>` or `<summary></summary>` | Defeats the purpose; at least write a meaningful one-liner |
| Writing `/// <summary>Gets or sets the {PropertyName}.</summary>` with no other information | Lazy — describe what the property represents, not what it syntactically is |

---

## Testing Projects Exception

Test projects are **exempt** from CS1591 enforcement. They set `<IsTestProject>true</IsTestProject>` in their `.csproj`, which the `Directory.Build.targets` condition `'$(IsTestProject)' != 'true'` skips.

Test project examples:
- [`RealmEngine.Core.Tests`](../../RealmEngine.Core.Tests/)
- [`Veldrath.Server.Tests`](../../Veldrath.Server.Tests/)
- [`Veldrath.Client.Tests`](../../Veldrath.Client.Tests/)
- [`Veldrath.Web.Tests`](../../Veldrath.Web.Tests/)
- [`Veldrath.GameClient.Core.Tests`](../../Veldrath.GameClient.Core.Tests/)
- [`Veldrath.GameClient.Components.Tests`](../../Veldrath.GameClient.Components.Tests/)
- [`Veldrath.Auth.Tests`](../../Veldrath.Auth.Tests/)
- [`Veldrath.Assets.Tests`](../../Veldrath.Assets.Tests/)
- [`Veldrath.Discord.Tests`](../../Veldrath.Discord.Tests/)
- [`RealmForge.Tests`](../../RealmForge.Tests/)
- [`RealmFoundry.Tests`](../../RealmFoundry.Tests/)
- [`RealmEngine.Data.Tests`](../../RealmEngine.Data.Tests/)
- [`RealmEngine.Shared.Tests`](../../RealmEngine.Shared.Tests/)

These projects do NOT generate documentation files and do NOT enforce CS1591. You can write test code without XML doc comments.

---

## Quick Checklist

When you add or modify a public member, run through this mental list:

- [ ] Did I add a public **class** or **record**? → Add `<summary>` on the type.
- [ ] Did I add a positional **record**? → Add `<param>` on each constructor parameter.
- [ ] Did I add a public **method**? → Add `<summary>`, `<param>` per parameter, `<returns>` (unless void).
- [ ] Did I add a public **property**? → Add `<summary>`.
- [ ] Did I add a public **constructor**? → Add `<summary>Initializes a new instance of <see cref="ClassName"/>.</summary>`.
- [ ] Did I add a public **field** or **constant**? → Add `<summary>`.
- [ ] Did I add a public **enum**? → Add `<summary>` on the type and on each member.
- [ ] Did I add a public **interface**? → Add `<summary>` on the interface and on each member.
- [ ] Did I add a public **delegate**? → Add `<summary>`.
- [ ] Did I add a **generic type parameter**? → Add `<typeparam name="T">`.
- [ ] Did my method throw an **exception**? → Add `<exception cref="...">`.
- [ ] Did I **override** an inherited member with no behavioral change? → Use `<inheritdoc/>`.

---

## See Also

- [`Directory.Build.targets`](../../Directory.Build.targets) — The enforcement mechanism.
- [`.github/instructions/blazor-component-development.md`](blazor-component-development.md) — Blazor-specific doc conventions for components.
