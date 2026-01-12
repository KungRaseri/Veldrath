# Modding Framework

**Last Updated**: January 12, 2026

---

## Overview

RealmForge will serve as the primary modding tool for RealmEngine games, enabling players to create, package, and distribute custom content. The modding framework is planned for v4.0 (Q4 2026).

---

## Mod Structure

### Mod Package Format

**`.realmmod` File:**
- Compressed ZIP archive
- Contains all mod files and metadata
- Digital signature support (optional)
- Compatible with RealmEngine mod loader

**Directory Structure:**
```
MyAwesomeMod/
├── mod.json                    # Mod manifest (required)
├── README.md                   # Mod documentation
├── LICENSE.txt                 # License information
├── CHANGELOG.md               # Version history
├── Data/                      # Game data files
│   ├── Items/
│   │   ├── Weapons/
│   │   │   ├── catalog.json
│   │   │   └── names.json
│   │   └── Armor/
│   │       ├── catalog.json
│   │       └── names.json
│   ├── Enemies/
│   │   ├── catalog.json
│   │   └── names.json
│   ├── Spells/
│   │   └── catalog.json
│   └── Abilities/
│       └── catalog.json
├── Scripts/                   # Custom C# scripts (v5.0+)
│   ├── ItemBehaviors/
│   ├── EnemyAI/
│   └── CustomMechanics/
├── Assets/                    # Media files (for Godot UI)
│   ├── Textures/
│   ├── Audio/
│   └── Models/
└── Localization/              # Translations
    ├── en-US.json
    └── es-ES.json
```

### Mod Manifest (mod.json)

**Required Fields:**
```json
{
  "id": "my-awesome-mod",
  "name": "My Awesome Mod",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds 50 new weapons and 10 new enemies",
  "realmEngineVersion": "3.0.0",
  "type": "content",
  "tags": ["weapons", "enemies", "items"],
  "website": "https://github.com/yourname/my-awesome-mod",
  "license": "MIT"
}
```

**Optional Fields:**
```json
{
  "dependencies": [
    {
      "modId": "another-mod",
      "version": ">=2.0.0",
      "required": true
    }
  ],
  "conflicts": [
    {
      "modId": "incompatible-mod",
      "reason": "Both mods modify weapon damage formulas"
    }
  ],
  "loadOrder": {
    "before": ["some-mod"],
    "after": ["base-game-expansion"]
  },
  "compatibilityNotes": "Works best with base game version 3.0.0 or later"
}
```

---

## Mod Creation Wizard (v4.0)

### Step 1: Mod Information

**User Inputs:**
- Mod ID (lowercase, no spaces, unique)
- Mod Name (display name)
- Author Name
- Version (semantic versioning)
- Description (markdown supported)
- Tags (for categorization)

**Validation:**
- Mod ID uniqueness check
- Valid semantic version format
- Non-empty required fields

### Step 2: Base Game Version

**Select Compatibility:**
- Target RealmEngine version
- Minimum required version
- Maximum supported version
- Compatibility notes

**Version Check:**
- Warn if targeting old version
- Display API changes since version
- Suggest migration if needed

### Step 3: Dependencies

**Select Required Mods:**
- Browse installed mods
- Add dependencies with version constraints
- Order dependencies by load priority
- Mark as required or optional

**Conflict Detection:**
- Check for known incompatibilities
- Warn about conflicting file paths
- Suggest resolution strategies

### Step 4: Content Type Selection

**Mod Templates:**
1. **Blank Mod** - Empty structure
2. **Item Pack** - Focus on items (weapons, armor, consumables)
3. **Enemy Pack** - Focus on enemies and encounters
4. **Quest Mod** - Focus on quests and storylines
5. **Overhaul Mod** - Balance changes, system modifications
6. **Total Conversion** - Complete game replacement

**File Generation:**
- Create folder structure based on template
- Generate empty catalog.json files
- Add example data (optional)
- Create README template

### Step 5: Initial Setup

**Configure Settings:**
- Enable/disable features (scripting, assets, localization)
- Set default metadata for new items
- Configure validation rules
- Choose icon for mod

**Summary:**
- Review all choices
- Preview folder structure
- Confirm and create project

---

## Mod Project Management (v4.0)

### Project Explorer

**File Tree:**
- Hierarchical view of mod files
- Icons by file type
- Context menu (add, delete, rename)
- Drag-and-drop file organization
- Search and filter files

**Dirty State:**
- Unsaved changes indicator
- Per-file dirty markers
- Save all command
- Revert changes option

### Mod Metadata Editor

**Edit mod.json:**
- Form-based editing
- Version management
- Dependency editor
- Tag management
- Preview manifest JSON

**Validation:**
- Real-time validation
- Error/warning indicators
- Required field enforcement
- Semantic version checking

---

## Asset Import (v4.0)

### Import JSON Files

**Sources:**
- Import from base game (copy to mod)
- Import from other mods (with permission)
- Import from file system
- Import from clipboard

**Conflict Resolution:**
- Detect existing files
- Options: Overwrite, Merge, Rename, Skip
- Preview changes before import
- Undo import

### Import Assets

**Media Files:**
- Drag-and-drop textures, audio, models
- Auto-organize by file type
- Validate file formats
- Optimize assets (compression, resizing)

**Batch Import:**
- Import multiple files at once
- Preserve folder structure
- Naming conventions
- Progress indicator

---

## Mod Validation (v4.0)

### Pre-Package Validation

**Checks:**
1. **JSON Compliance** - All JSON files valid v5.1 format
2. **Reference Integrity** - All references point to existing items
3. **Circular Dependencies** - No infinite reference loops
4. **Missing Assets** - All referenced assets exist
5. **Manifest Validity** - mod.json is valid
6. **Naming Conflicts** - No duplicate item IDs
7. **Value Ranges** - Stats within acceptable ranges
8. **Required Files** - All essential files present

**Validation Report:**
```
✅ JSON Compliance: Passed (42 files)
✅ Reference Integrity: Passed (156 references)
⚠️  Value Ranges: 3 warnings
   - Item "SuperSword" has damage=9999 (recommend <1000)
   - Enemy "GodBoss" has health=1000000 (recommend <100000)
   - Spell "Nuke" has manaCost=1 (suspiciously low)
❌ Missing Assets: 2 errors
   - Texture "custom-sword.png" referenced but not found
   - Audio "epic-music.mp3" referenced but not found
```

**Actions:**
- Fix errors before packaging
- Warnings can be ignored (not blocking)
- View detailed error messages
- Quick-fix suggestions

### Compatibility Checking

**Base Game Compatibility:**
- Check API version compatibility
- Detect breaking changes
- Suggest migrations
- Test against multiple game versions

**Mod Compatibility:**
- Check against installed mods
- Detect file conflicts
- Detect data conflicts (same item IDs)
- Suggest load order
- Generate compatibility report

**Performance Analysis:**
- Estimate impact on load times
- Check for excessive data duplication
- Detect inefficient patterns
- Memory usage projection

---

## Mod Packaging (v4.0)

### Export Settings

**Package Options:**
- Include source files (for modders)
- Include documentation
- Include screenshots
- Generate checksums (SHA256)
- Digital signature (optional)

**Compression:**
- Compression level (fast, balanced, max)
- Exclude patterns (.git, .vscode, etc.)
- File size limit warning

### Package Creation

**Process:**
1. Run validation suite
2. Create temporary staging directory
3. Copy mod files to staging
4. Generate checksums
5. Create ZIP archive with .realmmod extension
6. Sign package (if enabled)
7. Save to output location

**Output:**
```
MyAwesomeMod_v1.0.0.realmmod
MyAwesomeMod_v1.0.0.checksums.txt
MyAwesomeMod_v1.0.0_ReadMe.txt
```

**Package Metadata:**
```json
{
  "packageVersion": "1.0",
  "createdAt": "2026-12-15T10:30:00Z",
  "createdWith": "RealmForge v4.0",
  "fileCount": 42,
  "totalSize": "2.4 MB",
  "checksum": "sha256:abc123..."
}
```

---

## Mod Manager (v4.0)

### Installed Mods List

**Display:**
- Grid/list view of all mods
- Mod icon, name, version, author
- Enabled/disabled toggle
- Load order number
- Conflict indicators

**Sorting:**
- By name, author, version
- By load order
- By enabled status
- By install date

**Filtering:**
- By mod type
- By enabled/disabled
- By conflicts
- By tags

### Mod Details View

**Information:**
- Full description (markdown rendered)
- Screenshots/preview images
- Changelog
- Dependencies list
- Files list (expandable tree)
- Compatibility notes

**Actions:**
- Enable/disable mod
- Change load order
- View conflicts
- Open mod folder
- Uninstall mod
- Check for updates (future)

### Load Order Management

**Drag-and-Drop:**
- Reorder mods by dragging
- Visual indicators for drop zones
- Auto-resolve dependencies (move dependencies earlier)
- Conflict warnings during reorder

**Auto-Resolve:**
- Analyze dependencies
- Calculate optimal load order
- Handle circular dependencies
- Respect before/after hints

**Profiles:**
- Save load order as profile
- Quick-switch profiles
- Example: "Vanilla+", "Hardcore", "Testing"
- Export/import profiles

### Conflict Resolution

**Types of Conflicts:**
1. **File Conflicts** - Two mods modify same file
2. **Data Conflicts** - Two mods define same item ID
3. **Dependency Conflicts** - Incompatible dependency versions
4. **API Conflicts** - Mods require different game versions

**Resolution Strategies:**
- Load order priority (last wins)
- Merge patches (combine changes)
- Disable conflicting mod
- Choose winner manually

**Conflict Report:**
```
⚠️  2 conflicts detected

Conflict 1: File Overlap
- Mods: "Weapon Pack" vs "Armor Pack"
- File: Data/Items/catalog.json
- Resolution: Last in load order wins (Armor Pack)

Conflict 2: Data Collision
- Mods: "Magic Mod" vs "Custom Spells"
- Issue: Both define spell "fireball-v2"
- Resolution: Disable one or rename in mod
```

---

## Advanced Features (v5.0+)

### Script Editor

**C# Scripting:**
- Syntax highlighting
- IntelliSense for RealmEngine API
- Error detection
- Build and test in-editor
- Debug support

**Script Templates:**
```csharp
// Custom item behavior
public class CustomSwordBehavior : IItemBehavior
{
    public void OnEquip(Character character)
    {
        character.Attack += 50;
        character.AddStatusEffect("Flaming Weapon");
    }
}
```

### Visual Scripting

**Node-Based Editor:**
- Drag-and-drop nodes
- Connect inputs/outputs
- Visual flow of logic
- Export to C# code
- No coding required

**Node Types:**
- Events (OnEquip, OnAttack, OnKill)
- Conditions (If health < 50%)
- Actions (Deal damage, Heal, Apply buff)
- Variables (Store/retrieve values)

### Live Testing

**In-Editor Preview:**
- Test item effects
- Test enemy behaviors
- Test quest progression
- Test balance changes

**Integration:**
- Launch RealmEngine with mod enabled
- Hot-reload changes while testing
- Debug console overlay
- Performance profiling

---

## Distribution (v5.0+)

### Mod Repository

**Features:**
- Upload mods to central repository
- Browse and download community mods
- User ratings and reviews
- Download statistics
- Featured mods

**Submission:**
- Upload .realmmod package
- Add screenshots and description
- Select categories and tags
- Set visibility (public/private/unlisted)

### Auto-Update System

**Version Checking:**
- Check for mod updates on launch
- Notify user of available updates
- View changelog before updating
- One-click update

**Update Process:**
1. Download new version
2. Backup current version
3. Uninstall old version
4. Install new version
5. Update load order if needed
6. Rollback on error

---

## Best Practices

### Mod Organization
- Use clear, descriptive names
- Follow folder structure conventions
- Document your mod thoroughly
- Test on clean install
- Provide uninstall instructions

### Compatibility
- Declare all dependencies
- Test with popular mods
- Avoid hardcoded values
- Use references instead of names
- Version your mod properly

### Performance
- Avoid excessive file duplication
- Optimize assets (compress images)
- Minimize JSON file size
- Use references liberally
- Profile performance impact

### Community
- License your mod clearly
- Credit assets and inspiration
- Accept feedback gracefully
- Update regularly
- Support your users
