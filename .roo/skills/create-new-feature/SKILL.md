---
name: create-new-feature
description: Scaffold a new MediatR vertical-slice feature in RealmEngine.Core with command/query record, handler, FluentValidation validator, and a corresponding test file. Use when adding new game operations (commands) or read-only queries.
---

# Skill: create-new-feature

Scaffold a new vertical-slice feature in `RealmEngine.Core/Features/` following CQRS with MediatR patterns. Creates the command/query record, handler, FluentValidation validator, and a corresponding test file.

## Usage

Invoke this skill when you need to:
- Add a new game operation (e.g., equip item, cast spell, attack target)
- Add a read-only query (e.g., get character stats, list inventory)
- Refactor an existing feature into the MediatR pattern
- Follow the vertical-slice architecture consistently

## Requirements

- .NET 10 SDK installed
- Familiarity with MediatR, FluentValidation, and the existing feature structure (see [`RealmEngine.Core/Features/`](../../RealmEngine.Core/Features/) for examples)
- Working directory: `g:/code/Veldrath`

## Parameters

| Parameter | Example | Description |
|-----------|---------|-------------|
| `FeatureName` | `Equipment` | PascalCase name of the feature folder |
| `ActionName` | `EquipItem` | PascalCase name of the action (the command/query) |
| `ResponseType` | `EquipItemResult` | The result DTO the handler returns |
| `IsQuery` | `true`/`false` | Whether this is a query (read-only) or command (mutating) |

## Structure

Simple features use a flat layout; complex features (many actions) use a sub-namespace per action.

**Simple (flat):**

```
RealmEngine.Core/Features/{FeatureName}/
├── Commands/
│   ├── {ActionName}Command.cs      # IRequest<TResponse> record
│   ├── {ActionName}Handler.cs       # IRequestHandler<,> implementation
│   └── {ActionName}Validator.cs     # AbstractValidator<>
└── {ActionName}Result.cs            # Response DTO (if not nested)
```

**Complex (sub-namespace per action):**

```
RealmEngine.Core/Features/{FeatureName}/
└── Commands/
    └── {ActionName}/
        ├── {ActionName}Command.cs
        ├── {ActionName}Handler.cs
        └── {ActionName}Validator.cs
```

**Test file (mirrors source structure):**

```
RealmEngine.Core.Tests/Features/{FeatureName}/
├── {ActionName}CommandTests.cs
└── {ActionName}HandlerTests.cs
```

## Steps

1. **Study existing patterns** — Read one or more existing feature files to understand the conventions:

   - Simple example: [`RealmEngine.Core/Features/Equipment/Commands/EquipItemCommand.cs`](../../RealmEngine.Core/Features/Equipment/Commands/EquipItemCommand.cs) (if it exists) or another existing feature
   - Look for: `required` properties on records, `ILogger<T>` constructor injection in handlers, FluentValidation rule chains

2. **Create the command/query record** — Create `{ActionName}Command.cs`:

   ```csharp
   using MediatR;

   namespace RealmEngine.Core.Features.{FeatureName}.Commands;

   /// <summary>
   /// Command to {description of what this does}.
   /// </summary>
   /// <param name="...">Parameters go here.</param>
   public record {ActionName}Command : IRequest<{ResponseType}>
   {
       // Use 'required' keyword for mandatory parameters
       // Use 'init' accessors for immutability
   }
   ```

3. **Create the handler** — Create `{ActionName}Handler.cs`:

   ```csharp
   using MediatR;
   using Microsoft.Extensions.Logging;

   namespace RealmEngine.Core.Features.{FeatureName}.Commands;

   /// <summary>
   /// Handles <see cref="{ActionName}Command"/>.
   /// </summary>
   public class {ActionName}Handler : IRequestHandler<{ActionName}Command, {ResponseType}>
   {
       private readonly ILogger<{ActionName}Handler> _logger;

       /// <summary>Initializes a new instance of <see cref="{ActionName}Handler"/>.</summary>
       public {ActionName}Handler(ILogger<{ActionName}Handler> logger)
       {
           _logger = logger;
       }

       /// <inheritdoc />
       public Task<{ResponseType}> Handle({ActionName}Command request, CancellationToken cancellationToken)
       {
           // TODO: Implement business logic
           throw new NotImplementedException();
       }
   }
   ```

4. **Create the FluentValidation validator** — Create `{ActionName}Validator.cs`:

   ```csharp
   using FluentValidation;

   namespace RealmEngine.Core.Features.{FeatureName}.Commands;

   /// <summary>
   /// Validates <see cref="{ActionName}Command"/>.
   /// </summary>
   public class {ActionName}Validator : AbstractValidator<{ActionName}Command>
   {
       /// <summary>Initializes a new instance of <see cref="{ActionName}Validator"/>.</summary>
       public {ActionName}Validator()
       {
           // RuleFor(x => x.Property).NotEmpty();
           // RuleFor(x => x.Property).GreaterThan(0);
       }
   }
   ```

5. **Create the response DTO** — Create `{ActionName}Result.cs` (if the result type doesn't already exist):

   ```csharp
   namespace RealmEngine.Core.Features.{FeatureName};

   /// <summary>
   /// Result of the <see cref="Commands.{ActionName}Command"/>.
   /// </summary>
   public record {ActionName}Result
   {
       // Success indicator, resulting data, etc.
   }
   ```

6. **Create the test file** — Create `RealmEngine.Core.Tests/Features/{FeatureName}/{ActionName}Tests.cs`:

   ```csharp
   using FluentAssertions;
   using Xunit;

   namespace RealmEngine.Core.Tests.Features.{FeatureName};

   /// <summary>
   /// Tests for <see cref="Commands.{ActionName}Handler"/>.
   /// </summary>
   public class {ActionName}Tests
   {
       [Fact]
       public void Handle_Should_DoSomething()
       {
           // Arrange
           // Act
           // Assert
       }
   }
   ```

7. **Register services** (if needed) — If the handler requires new service registrations, add them to the relevant `ServiceCollectionExtensions.cs` or `Startup` configuration.

## Important Constraints

- **XML doc comments are required** on all public types and members (CS1591 is a compile-time error). Every command, handler, validator, and result must have `<summary>` comments.
- **Engine libraries must NOT depend on UI frameworks** — no Avalonia, ASP.NET Core MVC/Razor, or Blazor references in `RealmEngine.Core`, `RealmEngine.Shared`, or `RealmEngine.Data`.
- **No mocking frameworks** — prefer `FakeXxx` stub classes in `Infrastructure/` directories. Moq/NSubstitute are available only when stubs aren't practical.
- **`required` keyword** is preferred for mandatory constructor/init properties on command records.
- **Use primary constructors** where appropriate (C# 12+ pattern).

## Notes

- Handlers are automatically registered by MediatR via `AddMediatR()` scanning — no manual DI registration needed for handlers themselves (only for new infrastructure services).
- The FluentValidation pipeline behavior automatically validates commands before they reach the handler — no manual validation calls needed.
- Follow existing feature conventions by examining [`RealmEngine.Core/Features/`](../../RealmEngine.Core/Features/) for real examples.
- Test projects use xUnit + FluentAssertions with standard Arrange/Act/Assert layout.

## See Also

- [build-full](../build-full/SKILL.md) — Build after creating new feature to check CS1591 compliance
- [run-tests-by-component](../run-tests-by-component/SKILL.md) — Run tests for the engine component
- [`RealmEngine.Core/Features/`](../../RealmEngine.Core/Features/) — Existing features for reference
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Known handler patterns and constructor conventions
