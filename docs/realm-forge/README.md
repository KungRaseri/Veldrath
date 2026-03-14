# RealmForge — Content Editor

**Status**: Active Development
**Technology**: .NET 10 / Avalonia / ReactiveUI

## Overview

RealmForge is the official content management tool for RealmEngine. It connects directly to the Postgres database via ContentDbContext and provides a desktop UI for browsing and editing all game content entities.

## Architecture

- **UI**: Avalonia 11 with ReactiveUI view-models
- **Data access**: ContentDbContext (EF Core via RealmEngine.Data) — no JSON files on disk
- **Content tree**: Driven by ContentRegistry routing table (Domain → TypeKey → Entity)
- **Editor**: AvaloniaEdit JSON text view of DB entity fields; form mode for individual properties
- **References**: DB-backed reference picker via ContentRegistry

## Running

`powershell
dotnet run --project RealmForge
`

Requires a ContentDb connection string in ppsettings.json:

`json
{
  "ConnectionStrings": {
    "ContentDb": "Host=localhost;Database=realmengine;Username=...;Password=..."
  }
}
`
"@ | Set-Content "c:\code\RealmEngine\RealmForge\README.md" -Encoding UTF8

@"
# RealmForge — Developer Notes

**Last Updated**: March 2026
**Technology**: .NET 10 / Avalonia 11 / ReactiveUI / EF Core (Postgres)

## Overview

RealmForge is the content authoring tool for RealmEngine. It queries and modifies game content directly in Postgres via ContentDbContext. There is no JSON file editing — all content is DB-first.

## Key Services

| Service | Purpose |
|---|---|
| ContentTreeService | Builds left-panel hierarchy from ContentRegistry |
| ContentEditorService | Loads and saves typed entities from correct DbSet by table name |
| ReferenceResolverService | Builds @domain/path:item reference catalog from ContentRegistry |
| EditorSettingsService | Persists minimal editor preferences (theme) to local JSON |

## Long-term Direction

The current entity editor presents DB content as JSON text. In future versions this will be replaced by proper form-based editing. JSON export (DB → file) will be supported server-side for deployment pipelines.
