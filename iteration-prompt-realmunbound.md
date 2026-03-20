Continue iterating on the RealmUnbound projects (RealmUnbound.Server, RealmUnbound.Client, and RealmUnbound.Contracts).

Setup:
- Run `dotnet test RealmUnbound.slnx --filter Category!=UI` to establish the baseline. All work must finish with zero regressions against that baseline.

Goals — in priority order:
1. **Hub → MediatR bridge** — `GameHub` must dispatch real game actions to `RealmEngine.Core` handlers. Pick the highest-value feature slice that is (a) fully implemented in Core, (b) has a clear client trigger, and (c) can be tested with a mock mediator. Complete it end-to-end: hub method → ownership check via `IActiveCharacterTracker` → `mediator.Send(...)` → broadcast result to zone group. Typical first candidates: UseAbility, EnterDungeon, GainExperience.
2. **Stub and incomplete implementations** — fix before writing tests. Detect: hub methods that never call `mediator.Send(...)`; ViewModels with hardcoded data that should come from an HTTP service (e.g. `AvailableClasses`, `AvailableSpecies`); no-op commands (`ReactiveCommand.Create(() => { /* TODO */ })`); any `// TODO` or `throw new NotImplementedException()` in non-test code.
3. **Test coverage** — meaningful tests for real code that has none. For new hub dispatch methods add a test in `GameHubTests` with a mock mediator. For client services use the `FakeHttpHandler` pattern already in `RealmUnbound.Client.Tests`. Focus on success path, error/non-2xx path, and auth-required enforcement.

Process:
- Use the Explore subagent to run a gap analysis before writing any code. Identify P1 gaps (real code, zero tests) and P3 gaps (stubs, hardcoded data, no-ops) across both Server and Client. Cross-check `.github/copilot-memory/` so already-fixed items are not re-reported.
- Fix P3 stubs before writing tests — tests written against broken stubs have to be rewritten anyway.
- Every new `GameHub` dispatch method must: validate character ownership first; wrap the mediator call in try/catch and send an error message back to the caller on failure; broadcast the result to the zone group with `Clients.Group(zoneId)`, not just the caller.
- Every new or updated `HttpXxxService` method must be covered by a `FakeHttpHandler`-based test for success and non-2xx paths.
- Run `dotnet build RealmUnbound.slnx` and `dotnet test RealmUnbound.slnx --filter Category!=UI` after each batch of changes.
- After any change to `Program.cs` service registrations, verify DI completeness: every interface required by a Core handler or service that the server registers must have a concrete binding. The server uses `AddRealmEngineCore(p => p.UseExternal())` which skips all persistence-layer registrations — the server must explicitly register every `IXxxRepository` and `IXxxService` it needs. Known gaps that have already been fixed: `IInventoryService`, `IWeaponRepository`, `IMaterialRepository` (added 2026-03-19). If a new Core handler is added that injects an abstraction, check that Program.cs has a matching `AddScoped<IFoo, EfCoreFoo>()` call.

Wrap-up:
- If any non-obvious constraints, gotchas, or architectural decisions were discovered during the session, write them into the appropriate `.github/copilot-memory/` file (edit it directly using file tools). Only record things that would have caused wasted time if unknown at the start of a future session.
- Update `.github/copilot-memory/forge-foundry-sync.md` with what was completed and what remains.

Rules that must never be broken:
- Never suppress CS1591. Never add NoWarn entries for any project.
- Never create breadcrumb or placeholder files — finish the work or don't create the file.
- Never apply [Obsolete] — always move forward with new implementations.
- Never call `mediator.Send(...)` from a hub method without first verifying the character belongs to the caller via `IActiveCharacterTracker`.
- Never bypass SignalR authentication — hub methods that modify game state require an authenticated connection (JWT from query string is already wired in Program.cs).
- Engine libraries (Core, Shared, Data) must remain UI-agnostic — no Avalonia or SignalR references in those projects.
