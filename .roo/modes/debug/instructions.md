# RealmEngine — Debug Mode Instructions

## Common Debugging Tasks

### Build Errors
- **CS1591**: Missing XML doc comment on public type/member — add `<summary>` to fix. This is intentional enforcement, do not suppress.
- **MediatR registration errors**: Check `ServiceCollectionExtensions.cs` for missing handler registration
- **EF Core mapping errors**: Check `ContentModelConfiguration.cs` and `GameDbContext` for entity configuration

### Test Failures
- Tests use `FluentAssertions` (`result.Should().Be()`) not `Assert.Equal()`
- Server tests use `WebApplicationFactory<Program>` with SQLite in-memory — check `WebAppFactory.cs`
- Client UI tests need `--filter "Category!=UI"` to exclude headless tests requiring display
- Integration tests use `[Collection("Integration")]` — ensure test class has this attribute

### Runtime Debugging
- Server: ASP.NET Core + SignalR on default ports (check `launchSettings.json`)
- Client: Avalonia desktop app, connects to server via SignalR hub
- Debug SignalR hub calls by checking the Hub → MediatR bridge pattern
- Check `unbound-memory.md` for known handler quirks and blob schema details

### Common Gotchas
- `RarityTier` vs `ItemRarity` enum confusion — check which one is expected
- `PowerDto.School` may return raw values instead of expected display names
- `QuestDto` has partially-unmapped fields after JSON→DB migration
- Enemy/NPC dual-archetype behavior (same entity serves both roles)
- Castle.DynamicProxy/Moq constraints with internal types

## Memory Files
Always check `.github/agent-memory/` for known issues:
- `engine-codebase.md` — known handler quirks, open items
- `unbound-memory.md` — blob schema, P3/P4 status, session log
