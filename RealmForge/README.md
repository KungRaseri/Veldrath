# RealmForge — content editor

**Status:** ✅ Functional
**Technology:** Avalonia 11 + ReactiveUI (MVVM), EF Core (Npgsql)
**Target:** Desktop (Linux/macOS/Windows); edits the **Postgres** content database

## Overview

RealmForge is the editor for the game's content database. Every content type the
engine reads — abilities, spells, species, classes, backgrounds, items, materials,
enemies, zones, maps, languages, and so on — is stored as a row in Postgres, and
RealmForge is how that content is authored, reviewed, and fixed without hand-written SQL.

It is **not** a JSON editor and it is **not** Blazor/MAUI: the app is a normal Avalonia
desktop program ([`Program.cs`](Program.cs) → `AppBuilder.Configure<App>()`).

The player-facing counterpart is the SvelteKit app in [`web/`](../web/), whose `(foundry)`
route group handles community submissions. RealmForge is staff tooling and talks to the
database directly.

## Features

- **Content tree** — domains grouped into five activity-bar sections, driven by
  [`ContentDomainCatalog`](Services/ContentDomainCatalog.cs) (group → domain → sub-domain,
  e.g. `abilities/active/offensive`, `actors/species/humanoid`).
- **Entity editor** — reflection-driven field groups with typed rows, plus JSON editing for
  the blob columns (`Effects`, `Stats`, `Traits`, …).
- **Junction editing** — many-to-many tables (e.g. class → starting powers) get dedicated
  add/remove editors.
- **Map graph editor** — nodes, edges, and layout for the DB-backed map graph
  ([`MapGraphEditorService`](Services/MapGraphEditorService.cs), validated by
  `RealmEngine.Data/Validation/MapGraphValidator`).
- **Settings** — remembered database connection and editor preferences
  ([`Models/EditorSettings.cs`](Models/EditorSettings.cs)).

## Running

```bash
# from the repository root
dotnet run --project RealmForge
```

Requires a reachable Postgres instance with the game schema. The default connection in
[`appsettings.json`](appsettings.json) targets the local compose database:

```json
"ConnectionStrings": { "ContentDb": "Host=localhost;Port=5433;Database=veldrath;Username=veldrath;Password=veldrath_dev" }
```

Start it with `docker compose up -d postgres` (see [`docker-compose.yml`](../docker-compose.yml)).
Override the connection string with `ConnectionStrings__ContentDb` or in the app's settings.

## Project layout

```
RealmForge/
├── Program.cs / App.axaml        Avalonia entry point
├── Services/                     content catalog, editor + map-graph services, settings
├── ViewModels/                   ReactiveUI view models (one per screen/section)
├── Views/                        AXAML views
├── Models/                       editor settings
├── Converters/                   Avalonia value converters
├── Resources/ , Assets/          icons, brand imagery, RealmUI fonts
└── appsettings.json              ContentDb connection string
```

## Related

- [`RealmEngine.Data`](../RealmEngine.Data/) — EF Core persistence and the map graph validator.
- [`RealmEngine.Shared`](../RealmEngine.Shared/) — models the editor binds to.
- [`docs/design-system.md`](../docs/design-system.md) — tokens and typography used by the Avalonia surfaces.
- [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md) — why Foundry moved to the web app while RealmForge stayed native.
