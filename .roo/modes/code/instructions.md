# RealmEngine — Code Mode Instructions

## Critical Rules
- **CS1591 is a compile-time error**: Every publicly visible type/member in non-test projects MUST have XML doc comments (`<summary>` minimum). Never suppress CS1591.
- **Engine agnosticism**: `RealmEngine.Core`, `RealmEngine.Shared`, `RealmEngine.Data` must NEVER take a dependency on any UI framework (Avalonia, ASP.NET Core MVC/Razor, Blazor, etc.)
- **No mocking frameworks by default**: Prefer `FakeXxx` stub classes in `Infrastructure/` directories. Use Moq/NSubstitute only when stubs aren't practical.

## Coding Conventions
- `nullable enable` and `ImplicitUsings enable` globally
- C# latest — primary constructors, collection expressions in use
- New MediatR operations: `record CommandName : IRequest<ResponseDto>` + handler in same file under `Features/{FeatureName}/`
- Two organizational styles:
  - **Simple features**: flat layout — `Commands/{ActionName}Command.cs`, `Handlers/{ActionName}Handler.cs`
  - **Complex features**: sub-namespace per action — `Commands/{ActionName}/{ActionName}Command.cs`, `Commands/{ActionName}/{ActionName}Handler.cs`
- Avalonia ViewModels: `RaiseAndSetIfChanged` for bindable properties, `ReactiveCommand.CreateFromTask` for async, `WhenAnyValue` for derived state
- Only comment the *why*, not the *what*

## DI Registration Pattern
- `ServiceCollectionExtensions.cs` in each project provides `Add{Project}()` methods
- `AddRealmEngineCore()` — registers all services
- `AddRealmEngineMediatR()` — full assembly scan for handlers
- Granular registration available: `AddGenerationHandlers()`, `AddCharacterCreationHandlers()`, `AddCatalogHandlers()`, `AddGameplayHandlers()`, `AddSaveLoadHandlers()`

## Doc Comment Format
```csharp
/// <summary>Brief description.</summary>
/// <param name="foo">What foo represents.</param>
/// <returns>What the return value means.</returns>
```

## Key File Locations
- Feature handlers: `RealmEngine.Core/Features/{FeatureName}/`
- Pipeline behaviors: `RealmEngine.Core/Behaviors/`
- DI registration: `RealmEngine.Core/ServiceCollectionExtensions.cs`
- Build config: `Directory.Build.props`, `Directory.Build.targets`
- Package versions: `Directory.Packages.props`
