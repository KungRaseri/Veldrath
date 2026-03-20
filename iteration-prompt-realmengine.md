Continue iterating on the RealmEngine library projects (RealmEngine.Core, RealmEngine.Shared, RealmEngine.Data).

Setup:
- Run `dotnet test RealmEngine.slnx` to establish the baseline. All work must finish with zero regressions against that baseline.

Goals — in priority order:
1. **Feature complete** — identify any features that exist in the engine but have partial, stubbed, or missing handler/service implementations. Finish them end-to-end.
2. **Well tested** — meaningful coverage of new and existing code. Not 100% — focus on behavior at boundaries, error paths, and non-trivial logic. Never leave placeholder test files.
3. **Well documented** — every public type and member must have an XML `<summary>` (and `<param>`/`<returns>` where meaningful). This is a hard build rule enforced by CS1591.

Process:
- When running the gap analysis, first read `.github/copilot-memory/gap-analysis-process.md`
  and use the prompt template and priority tiers defined there.
- Use the Explore subagent to run a gap analysis before writing any code. Identify: incomplete feature implementations, untested handlers/services, missing XML docs.
- Prioritize gaps from most impactful to least.
- Fix any P3 stub/placeholder implementations before writing new tests — tests
  written against broken stubs have to be rewritten anyway.
- Run `dotnet build RealmEngine.slnx` and `dotnet test RealmEngine.slnx` after each batch of changes.

Wrap-up:
- If any new non-obvious constraints, gotchas, or architectural decisions were discovered during the session, write them into the appropriate `.github/copilot-memory/` file (edit it directly using file tools). Only record things that would have caused wasted time if unknown at the start of a future session.

Rules that must never be broken:
- Never suppress CS1591. Never add NoWarn entries.
- Never create breadcrumb or placeholder files — finish the work or don't create the file.
- Never apply [Obsolete] attribute to members/classes — we always move forward with new implementations/work and do not support backwards compatibility