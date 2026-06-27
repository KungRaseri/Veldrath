# RealmEngine — Test Engineer Mode Instructions

## Testing Framework
- **xUnit** + **FluentAssertions** (not Jest)
- 11 test projects, ~8,500+ tests
- Standard layout: **Arrange / Act / Assert** with FluentAssertions

## Test Organization
- Tests mirror source structure path-for-path:
  - Source: `RealmEngine.Core/Features/Combat/Commands/AttackEnemy/AttackEnemyHandler.cs`
  - Test: `RealmEngine.Core.Tests/Features/Combat/Commands/AttackEnemyHandlerTests.cs`

## Key Conventions
- `[Fact]` / `[Theory]` with `[InlineData]` for parameterized tests
- `[Trait("Category", "...")]` for filtering: `Feature`, `UI`, `ViewModel`, `Integration`
- Client/Forge UI tests need `--filter "Category!=UI"` (headless tests require display)
- Server integration tests use `[Collection("Integration")]` + `WebAppFactory`
- Prefer `FakeXxx` stubs over mocking frameworks; Moq/NSubstitute as fallback

## Test Project Summary
| Project | Framework | DB Provider |
|---------|-----------|-------------|
| Core.Tests | xUnit + Moq | InMemory |
| Data.Tests | xUnit | InMemory |
| Shared.Tests | xUnit | — |
| Server.Tests | xUnit + Moq | SQLite |
| Client.Tests | xUnit + Avalonia.Headless | — |
| Forge.Tests | xUnit + Avalonia.Headless | — |
| Foundry.Tests | xUnit + bunit | — |

## Running Tests
```powershell
dotnet test Realm.Full.slnx                              # All tests
dotnet test RealmEngine.slnx                             # Engine only
dotnet test Veldrath.Server.Tests                        # Server only
dotnet test Veldrath.Client.Tests --filter "Category!=UI"  # Client (no UI)
dotnet test Realm.Full.slnx --filter "FullyQualifiedName~AttackEnemy"  # Specific test
dotnet test Realm.Full.slnx --collect "XPlat Code Coverage" --settings coverage.runsettings  # With coverage
```

## Coverage
- Per-component Codecov flags (engine, client, server, forge, discord, foundry, web)
- Exclusions in `coverage.runsettings`
- `[ExcludeFromCodeCoverage]` on: Program, App, XAML resources, thin wrappers
- Every test project has `[assembly: ExcludeFromCodeCoverage]` in `AssemblyInfo.cs`
