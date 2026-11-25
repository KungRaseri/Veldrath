# Content packs

A **content pack** is a directory of YAML files that the content catalogue is imported from. Before packs, content
lived only in the C# seeders (`RealmEngine.Data/Seeders/`), and those are insert-only: they collect the slugs that
already exist and insert what is missing, so editing a seeded record changes nothing on a database that already has
that slug. Packs are the authoring surface that can be edited, diffed, reviewed and re-imported.

The seeders remain the initial catalogue. A pack that uses an existing slug updates that row; it never inserts a
second one.

## Commands a content author runs

From the repository root, with the .NET 10 SDK installed:

```bash
# Check the pack directory. Exits 1 on any Error, 0 when only warnings were found.
dotnet run --project Veldrath.ContentPack -- validate content/

# Import every family: one line per directory under content/, in the order a reference can point. An
# archetype names the loot table it drops and a zone location names the archetypes it spawns, and a
# pack cannot resolve a GUID for a row it creates in the same run (see "Two runs, in dependency order").
# Needs the content database; the argument can also come from $CONTENT_DATABASE.
DATABASE="Host=localhost;Database=veldrath;Username=veldrath;Password=***"
dotnet run --project Veldrath.ContentPack -- import content/items --database "$DATABASE"
dotnet run --project Veldrath.ContentPack -- import content/loottables --database "$DATABASE"
dotnet run --project Veldrath.ContentPack -- import content/archetypes --database "$DATABASE"
dotnet run --project Veldrath.ContentPack -- import content/zonelocations --database "$DATABASE"
dotnet run --project Veldrath.ContentPack -- import content/languages --database "$DATABASE"

# See what an import would do without writing anything: add --dry-run to any line above. A dry run only
# reads the stored catalog, so run it after the import has landed — it cannot stand in for a family whose
# prerequisite rows are not stored yet.
```

`validate` never needs a database: it seeds the tracked seeders into an in-process provider, so references to seeded
content resolve exactly as they would after an import. If a pack must be checked against a live catalogue instead,
pass `--database` to `validate` as well.

`content/` holds five families at the moment — `items`, `loottables`, `archetypes`, `zonelocations` and `languages` —
and a family missing from that list is never read: the run that omits it prints `Import complete: the catalog already
matched the pack.` whether or not its file changed. `content/items/` is the worked example rather than world
content, so its line is what lands the two `example-` rows; no world content references them.

## The file convention

```
content/<family>/<zone-or-batch>.yaml
```

- **One family per file.** The directory name is the family: `content/items/…` holds items, `content/quests/…` holds
  quests. A plural directory is fine — it resolves to the family the engine registers (`items` → `item`).
- **Known families** are the content types `ContentSchemaRegistry` advertises: `item` (`weapon`, `armor`), `ability`
  (`spell`, `power`), `species`, `class`, `archetype`, `instance`, `background`, `skill`, `material`,
  `materialproperty`, `enchantment`, `quest`, `recipe`, `loottable`, `organization`, `zonelocation`, `dialogue`,
  `language`.
- **Slug is the identity**, and must match `^[a-z0-9]+(-[a-z0-9]+)*$`. Two entities with the same slug in the same
  family are an error.
- A `family:` header is optional; when present it must agree with the directory.

## The file shape

```yaml
# content/items/example.yaml
family: item
entities:
  - slug: example-shortblade
    typeKey: heavy-blades
    displayName: Example Shortblade
    itemType: weapon
    weaponType: sword
    stats:
      damageMin: 6
      damageMax: 11
      weight: 3.0
    traits:
      stackable: false
```

An entity is the **entity's own payload**, in the same field names and value types the content schema advertises —
there is no pack-specific schema, and the validators registered in `RealmEngine.Data/Validation/Content/` are the
only rule set. Plain YAML scalars keep their JSON type (`2.5` is a number, `true` is a boolean, `"007"` is text
because it is quoted); literal and folded scalars are always text.

### GUID foreign keys are written as slugs

A few references are stored as GUIDs rather than slugs, because the seeder resolves them by lookup. A pack writes the
slug and the importer resolves it:

| Family | Field | Points at |
|---|---|---|
| `archetype` | `speciesId` | `species` |
| `archetype` | `classId` | `class` |
| `archetype` | `backgroundId` | `background` |
| `archetype` | `lootTableId` | `loottable` |
| `instance` | `archetypeId` | `archetype` |
| `instance` | `lootTableOverride` | `loottable` |

An unknown slug there is an error that names the field and the missing target. Such a reference must point at content
that already exists — a pack cannot resolve a GUID for a row it is creating in the same run.

### Two runs, in dependency order

A directory can declare both sides of a reference: `content/archetypes/` names the loot table it drops, and
`content/loottables/` authors that table. One import cannot bind them — the GUID does not exist until the table is
written — so a directory that spans both is imported family by family, named families first. That is why the
quick-start above is five commands and not one: `import content/` validates the whole directory before it writes
anything, so on a fresh catalogue it reports each archetype's `lootTableId` unresolved and writes nothing.

```bash
# The pair that shares a GUID reference, in order. The full list is under "Commands a content author runs".
dotnet run --project Veldrath.ContentPack -- import content/loottables --database "..."
dotnet run --project Veldrath.ContentPack -- import content/archetypes --database "..."
```

`content/zonelocations/` names an archetype per pool entry by slug rather than by GUID, but the constraint has the
same shape: those archetype rows have to be stored, so that family follows `content/archetypes/`.

`validate` does not need that split, and it does not need a database. Against its in-process seed it stages the
directory **family by family, in the order a reference can point**: a family whose GUID foreign key names another
family is checked after the family that authors it. That is why `validate content/` is clean on the shipped tree even
though 30 of the 32 archetypes name a table no seeder creates. Staging is not leniency — a slug no family in the
directory creates is still reported as `pack-reference-unresolved`.

`validate --database` keeps the single read-only pass: the catalogue it is handed is the live one, so it is never
written to, and a directory validated that way must already have had its prerequisite families imported.

## What an import does

For each entity, by slug:

| Stored | Pack | Result |
|---|---|---|
| nothing | the entity | **created** |
| identical values | the entity | **unchanged** — no UPDATE is issued |
| different values | the entity | **updated** in place: the row keeps its id (live game data points at it), its `Version` is incremented and `UpdatedAt` is stamped |

The summary prints `created / updated / unchanged` per family and in total. Importing the same pack twice therefore
reports zero created and zero updated the second time.

An update reconciles the entity's child rows as well as its columns: a child the pack still declares is matched to the
stored row (by the key the author writes where there is one, by its authored values otherwise), a child the pack adds
is inserted, and one it drops is deleted. A child nobody touched is left alone, so editing a loot table's display name
does not churn its entries, and re-importing after an edit does not duplicate them.

Validation runs first, over the whole pack. **Any Error-severity issue means nothing is written** — a pack is one
unit, and half of it landing in the catalogue would be worse than none of it. Warnings are printed and do not block.

An import writes `ContentDbContext` only. `ApplicationDbContext` — live game data — is never touched.

## What the guard checks

`.github/workflows/ci-content.yml` runs `validate content/` on every pull request that touches a pack, the content
model or the tool, and fails the job on any Error. A red job names the rule, the entity slug and the field:

```
Error   missing-quest-target [hunt-the-unwritten]: hunt-the-unwritten field 'objectives[0]' points at unknown
        actor 'no-such-slug'. (field: objectives[0])
```

Warnings fail nothing: they are printed and counted.

## Limits worth knowing

- **An import owns the whole entity.** A pack entity replaces the stored row's authored values, including child
  collections, so an author who writes an archetype must write the power pool they intend it to keep.
- **Junction rows with GUID children are not authored yet.** `archetypePowerPool`-style junctions need row ids a
  pack cannot express; that is a follow-up, not a silent default.
- **The content registry is not maintained by the importer.** `ContentRegistry` rows still come from the seeder that
  owns them; the write endpoints do not maintain it either.

## Changing what a town sells (a catalogue family with no pack)

`ZoneStockItems` — the rows that say which items a town's merchants offer — is a catalogue family with no pack: a row
is a zone↔item pair with no slug and no display name, so it is absent from `ContentEntityTypes`, the Foundry schema and
the importer on purpose, and `RealmEngine.Data/Seeders/ZoneStockSeeder` is the only thing that writes it. The other
tables with no pack family are skipped for the same structural reason — none of them is a `ContentBase`, so the
importer's `FindBySlugAsync` has nothing to match. That covers `ZoneLocationConnection`, a join
table keyed by `FromLocationSlug`/`ToLocationSlug` (`ZoneLocationConnection.cs:14`, `:20`), and the map graph's
`Map`/`MapNode`/`MapEdge` — a map document whose maps and nodes are keyed by authored string ids
(`ContentModelConfiguration.cs:310`, `:328`), while each directed edge is keyed by a generated `uuid` and identified at
authoring time by its natural slot `(MapId, FromNodeId, Direction)` (`:345`, `:352`) — and the vocabulary and
configuration tables, `ContentRegistry`, `TraitDefinitions`, `NamePatternSets` and `GameConfig`, which are not
per-entity content at all.

**For a stock shelf, the decided procedure is a data migration — not a pack, and not a reconciling seeder.** Adding a
row needs no migration at all: the seeder inserts every authored row the database is missing, on the next start
(`Program.cs:416` → `DatabaseSeeder.SeedAsync` → `ZoneStockSeeder.SeedAsync`). Removing a row, or changing a row's
`SortOrder`, needs a migration, because an insert-only seeder can never take a row away. (The map graph is the same
kind of table family with a different seeder contract: `MapGraphSeeder` is bootstrap-only, so *every* graph change is a
migration. See the map-graph section below.)

1. Edit `Authors()` in `RealmEngine.Data/Seeders/ZoneStockSeeder.cs`. That array is the truth for a database that has
   never been seeded, and it is what the next fresh database gets.
2. Add a migration after the last one in `RealmEngine.Data/Migrations/`:

   ```bash
   dotnet tool restore
   dotnet restore RealmEngine.Data   # dotnet ef aborts with NETSDK1004 without restored assets
   dotnet ef migrations add ReconcileZoneStockShelves --context ContentDbContext \
     --project RealmEngine.Data --output-dir Migrations
   ```

   A shelf edit changes no model, so `git status` must show exactly the two new files and
   `ContentDbContextModelSnapshot.cs` must come back unchanged. If the snapshot changed, your edit touched more than
   the shelf. (The generated `.Designer.cs` is a copy of the snapshot with a different class name and `[Migration]`
   id — that is expected, and it is not the thing to compare.)
3. Replace the generated `Up` with one statement per removed pair. The `(ZoneId, ItemSlug)` pair is a row's only
   authored identity — `Id` is a generated `Guid`, so a migration cannot name the row it means to remove:

   ```csharp
   /// <inheritdoc />
   protected override void Up(MigrationBuilder migrationBuilder)
   {
       // One statement per pair the shelf no longer lists. Postgres and SQLite both accept this quoted form.
       migrationBuilder.Sql(
           "DELETE FROM \"ZoneStockItems\" WHERE \"ZoneId\" = 'crestfall' AND \"ItemSlug\" = 'hunters-bow';");
   }

   /// <inheritdoc />
   protected override void Down(MigrationBuilder migrationBuilder)
   {
       // Nothing to restore: the row's Id was generated on insert, so a Down can re-insert the pair but cannot give
       // it back the same Id. Revert the seeder edit and let the next start insert the missing rows.
   }
   ```

   A reorder is a removal of the same kind: `Rows()` sets `SortOrder = (index + 1) * 10`
   (`ZoneStockSeeder.cs:149`), so deleting the `(ZoneId, ItemSlug)` row makes the seeder re-insert it at its new index
   on the next start. A plain `SortOrder` update in the migration would work too, but the `DELETE` is the one shape
   you need anyway.
4. Verify against a real Postgres, not on faith. Boot the server once (it migrates the content database at
   `Program.cs:405-408` and then seeds), then:

   ```sql
   SELECT "ZoneId", "ItemSlug", "SortOrder" FROM "ZoneStockItems" ORDER BY "ZoneId", "SortOrder";
   ```

   That result must equal `Authors()`. No test covers a migration — `RealmEngine.Data.Tests` builds its schema with
   `EnsureCreated()`, and `MigrateAsync` only runs on a Postgres content database — so this query is the evidence and
   the before/after counts belong in the pull request body.

**Who owns a shelf.** `Authors()` owns it. A database that has never been seeded gets it from the seeder; a database
that already has one gets the difference from a migration; nothing else writes these rows. A stored row `Authors()`
no longer lists is drift, and the remedy is always a migration — never a seeder edit on its own.

## Changing the authored map graph (a document family with no pack, and a bootstrap-only seeder)

`Map`/`MapNode`/`MapEdge` have no pack family for the same structural reason as stock: none of them is a `ContentBase`,
so `FindBySlugAsync` has nothing to match. Their identity is not uniform, and the difference matters when writing a
migration:

- a **map** is keyed by its authored string id (`zone:tolvaren`, `region:saltcliff`), and `(MapType, OwnerId)` is
  unique (`ContentModelConfiguration.cs:310`);
- a **node** is keyed by its authored string id (`zone:tolvaren:exit:zone:sunken-name`) and belongs to exactly one map
  (`:328`);
- an **edge** is keyed by a generated `uuid`, and identified for authoring by its **natural slot** — `(MapId,
  FromNodeId, Direction)` is unique (`:345`, `:352`). Seeding the same authored graph twice produces the same slots and
  different edge uuids, so a uuid is never graph identity across databases.

`MapGraphSeeder` is **bootstrap-only**: it returns immediately when `Maps` already has a row
(`RealmEngine.Data/Seeders/MapGraphSeeder.cs:21-22`). The server applies content migrations before it seeds
(`Program.cs:409`, `:416`), so a database that was seeded once keeps that graph forever; no restart, reseed or
`WorldCanonSweep` (which rewrites map/node *text* only, `WorldCanonSweep.cs:169-212`) changes its topology. Foundry is
per-instance authoring, not how a repository change reaches other instances: `MapEditorService.SaveAsync`
(`MapEditorService.cs:181`) saves a whole candidate map and bumps `Version`, and a seeded map has no creator, so saving
one requires `content.manage` rather than ordinary reviewer status.

**A repository-authored graph change ships as a reviewed, data-only `ContentDbContext` migration.** Not a pack, not a
seeder edit, not a Foundry replay, and not a reset.

1. Edit `MapGraphSeeder.cs` first. It stays the truth for a database that has never been seeded, and it is what the
   next fresh database gets.
2. Scaffold the migration in `RealmEngine.Data/Migrations/`. A graph edit changes no model, so `git status` must show
   exactly the `.cs` migration and its `.Designer.cs`, and `ContentDbContextModelSnapshot.cs` must come back unchanged:

   ```bash
   dotnet tool restore
   dotnet restore RealmEngine.Data   # dotnet ef aborts with NETSDK1004 without restored assets
   dotnet ef migrations add BackfillSeededMapDoorsAndArrivalHops --context ContentDbContext \
     --project RealmEngine.Data --output-dir Migrations
   ```
3. Freeze the rows as literals copied from the seed diff; **never call the seeder from the migration**. An edge keeps
   its authored `uuid` in the frozen row, but the natural slot is its identity when patching an existing database.
4. Guard, then write, in the one transaction Entity Framework opens for the migration:
   - Return when `Maps` is empty — the seeder creates the graph, doors included, on a fresh database.
   - Patch only named **eligible** source maps: present, `IsActive`, `CreatedByAccountId IS NULL`, and the expected
     `MapType`/`OwnerId`. Report absent, inactive and user-owned maps in the preflight and leave them exactly as they
     are — never recreate or reactivate one.
   - Verify before writing: every required endpoint belongs to its map; every exit node's `TargetMapId` names a live
     map and its `TargetNodeId` exists there; every region marker still carries the `TargetMapId` the new Arrival hop is
     meant to reach.
   - An existing node id with equivalent topology (map/type/ref/exit/target/visibility/unlock semantics) satisfies the
     patch and is left untouched, text and layout included.
   - An existing edge at the same natural slot satisfies the patch even with another uuid, provided from, direction,
     to, type, visibility and traversability match; keep its uuid and its label.
   - A seed-time walk the new door displaces may be **re-homed** only when the patch declares it: the source slot must
     hold exactly that walk with the expected type/visibility/traversability, the destination slot must be free (or
     already hold the moved walk on a replay), and the row keeps its uuid and label. `#270` needs one — Aldenmere's
     market walk to the Grey Cup moves East→North so the moor-road door can take East, which is where a fresh seed puts
     it. Anything else is a conflict.
   - A conflict — an incompatible node id, an incompatible uuid, an occupied slot holding a different edge, a missing
     required endpoint or target — aborts the whole migration with the exact identity. Never `ON CONFLICT DO NOTHING`,
     never silently pick another direction, and never leave half a door recorded.
   - Bump `Version`/`UpdatedAt` once per changed map, and preserve activation, authorship and review fields.
   - Replaying the body is a no-op: every row is satisfied and no map is bumped a second time.
5. `Down` deletes nothing. The rows may have been edited in Foundry since, and an inserted door cannot be told apart
   from an authored one. Roll data back by restoring the pre-migration backup, or ship a reviewed compensating
   migration that names the exact ids and slots — do not write a blind `DELETE` into `Down`.

**Rollout and proof.** Preflight on a restored copy (run the `Up` body inside a transaction and roll it back), then
quiesce other writers and instances while the migration applies. The committed proof is
`scripts/verify-map-graph-upgrade.sh`: it seeds disposable PostgreSQL 17 with the real server from before the change,
proves the rows were absent, starts the new server on that same database with no reseed or reset, and asserts the
persisted rows, the migration history, unrelated authoring and activation/review survival, restart idempotence, a fresh
database, an equivalent pre-existing node/edge carrying another uuid, and that a hostile slot or node aborts with no
partial writes and no history row. Unit tests cannot cover this: the test factories build their schema with
`EnsureCreated()` and never run a migration.

The first such migration is `BackfillSeededMapDoorsAndArrivalHops`, which carries the twelve zone doors from #270 and
the two region Arrival hops from #282.
