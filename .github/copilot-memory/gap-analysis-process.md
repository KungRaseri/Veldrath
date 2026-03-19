# RealmEngine — Gap Analysis Process

## When to Invoke
At the start of every iteration session, before writing any code. Use the Explore subagent
with thoroughness **thorough**.

## Prompt Template (copy-paste and adapt)

```
Thorough gap analysis of RealmEngine.Core, RealmEngine.Shared, and RealmEngine.Data.
Cross-check .github/copilot-memory/ for items already fixed before reporting them.

Return findings ranked by priority tier:

**P1 — Handler/service ships to runtime, has zero direct unit test coverage.**
  - List handler class name, file path, and the command/query it handles.
  - Note if an integration test covers it indirectly (lowers urgency but still P1).

**P2 — Service or handler has complex non-trivial logic but significant untested paths.**
  - Focus: branching logic, error returns, state mutation, calculations.
  - Skip handlers whose only untested path is a trivial null-guard already covered by pattern.

**P3 — Partial/stub implementations that ship incomplete behavior.**
  - Detect: hardcoded return values (e.g. `return 0`, `return []`), methods that always
    return a constant regardless of input, logic blocks guarded by `// TODO` comments,
    `throw new NotImplementedException()`, or empty async methods that do nothing.
  - Do NOT flag this as a test gap — flag it as an *implementation* gap needing finishing.

**P4 — XML doc gaps (CS1591).**
  - List file + member. Only report if the build would actually fail (non-test project,
    public visibility). Do not report test project members.
```

## Stub/Partial Detection Heuristics
Search source for these patterns — each is a strong signal of incomplete work:
- `return new ...Result { Success = false }` with no preceding validation (default failure)
- `// TODO`, `// STUB`, `// PLACEHOLDER`, `// Not implemented`
- Method body is only `return Task.CompletedTask` or `return Task.FromResult(default(T)!)`
- Private helper that returns a hardcoded collection or constant with no real logic

## Stale-Result Reduction
Before reporting any gap, check whether a matching test file exists in the corresponding
`*.Tests` project under the same `Features/` subfolder. If a test class name contains
the handler name, treat direct coverage as confirmed and downgrade or skip the gap.

## After the Analysis
- Implement P3 (stubs) before writing new tests — tests against broken stubs are useless.
- Work P1 gaps in order: handlers used by the game server first, pure query handlers last.
- Batch P4 doc fixes into one pass after functional work is done.
