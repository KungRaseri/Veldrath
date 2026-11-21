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

# Import it. Needs the content database; the argument can also come from $CONTENT_DATABASE.
dotnet run --project Veldrath.ContentPack -- import content/ \
  --database "Host=localhost;Database=veldrath;Username=veldrath;Password=..."

# See what an import would do without writing anything.
dotnet run --project Veldrath.ContentPack -- import content/ --database "..." --dry-run
```

`validate` never needs a database: it seeds the tracked seeders into an in-process provider, so references to seeded
content resolve exactly as they would after an import. If a pack must be checked against a live catalogue instead,
pass `--database` to `validate` as well.

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
