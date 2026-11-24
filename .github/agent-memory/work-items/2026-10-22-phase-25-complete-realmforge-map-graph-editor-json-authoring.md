---
kind: work-item
date: 2026-10-22
area: map
title: "Phase 25 complete: RealmForge map graph editor + JSON authoring"
---

### Session-47 (2026-10-22) — Phase 25 complete: RealmForge map graph editor + JSON authoring

**Engine (RealmEngine.Data)**
- `Authoring/MapGraphJsonSerializer.cs`: `MapGraphFile(FormatVersion=1, ExportedAt, Maps)`;
  camelCase + indented + string enums + deterministic ordering; import rejects empty or
  malformed JSON, unsupported versions, and duplicate map ids (`InvalidDataException`).
- 9 tests in `RealmEngine.Data.Tests/Authoring/MapGraphJsonSerializerTests.cs`.

**RealmForge**
- `Services/MapGraphEditorService.cs`: `IServiceScopeFactory` + `ContentDbContext` with a
  nullable `GetService` so the tool runs without a configured database. Save performs
  explicit node/edge sync (`db.MapNodes.Remove` / `db.MapEdges.Remove` for stale rows),
  versions start at 1 and increment on update, and export/import support strings and files
  with an overwrite toggle.
- `ViewModels/MapGraphEditorViewModel.cs` + `MapNodeRowViewModel` + `MapEdgeRowViewModel`:
  map picker, metadata, node/edge editing, add/remove (removing a node removes its edges),
  validation list, save/delete, and import/export. `BuildGraph()` returns `MapGraph`.
- `Views/MapGraphEditorView.axaml(.cs)`: code-behind owns the Avalonia StorageProvider file
  pickers; the details pane edits the selected node or edge.
- `MainWindowViewModel.OpenMapGraphEditorCommand` + **Maps** toolbar button; the VM calls
  `LoadMapsAsync()` explicitly (no fire-and-forget async in its constructor).
- DI: `MapGraphEditorService` registered as a singleton in `App.axaml.cs`.

**Testing gotcha**
- Registering an EF Core InMemory database through
  `AddDbContext(o => o.UseInMemoryDatabase($"db-{Guid.NewGuid():N}"))` executes the lambda
  per scope, so every scope gets a different database. Capture the database name once
  outside the lambda. This caused `SaveAsync` followed by `LoadAsync` to return null.

**Verification**
- `Realm.Full.slnx`: 0 warnings / 0 errors.
- Full suite: **4,753 passing**, 0 failed across 13 projects (RealmForge 27, Data 262,
  Server 759, Client 663, Core 1,944, Shared 803, Components 80, others).
- RealmForge ran 20 s without crashing; the expected Postgres connection error is handled
  and the window stays up when the content database is offline.
- The text-dashboard/map-graph effort is complete; the plan was later folded into
  `docs/client-ui-architecture.md` and deleted.

---
