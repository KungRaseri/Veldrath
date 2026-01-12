# RealmForge - Game Data Editor & Modding Tool

**Version**: 3.0 (Blazor Hybrid)  
**Last Updated**: January 12, 2026  
**Technology**: .NET MAUI Blazor Hybrid (.NET 9.0)  
**Purpose**: Visual editing and modding tool for RealmEngine game data

---

## Overview

**RealmForge** is a .NET MAUI Blazor Hybrid desktop application that provides form-based visual editing of JSON game data files with a dual-mode interface (Form/JSON toggle). It serves as both a JSON data editor and the primary modding tool for RealmEngine games.

### Why RealmForge?

- **Dual-Mode Editing**: Form mode for structured editing, JSON mode for advanced users
- **Model-Driven Forms**: Auto-generated forms from RealmEngine.Shared models
- **Error Prevention**: Type-safe form inputs prevent syntax and type errors
- **No JSON Knowledge Required**: Non-technical users can edit game data via forms
- **Direct Model Integration**: Uses actual RealmEngine models (no schema drift)
- **Modding Support**: Create, package, and distribute game mods
- **Cross-Platform Ready**: Windows, macOS, Linux (via .NET MAUI)

---

## Current Features (v3.0)

### 1. Dynamic Form Editor ✅

**Reflection-Based Form Generation:**
- Automatically generates forms from any RealmEngine.Shared model
- Type-specific inputs for all property types:
  - `string` → Text input
  - `int` → Number input
  - `double`/`float` → Decimal number input
  - `bool` → Checkbox
  - `enum` → Dropdown select
  - Complex types → Read-only display (nested objects/lists)

**Features:**
- Automatic label generation (PascalCase → Title Case)
- Property-level validation support
- Real-time state updates
- Save/Cancel actions with callbacks

**Supported Models:**
- Items (weapons, armor, consumables)
- Enemies (humanoid, beast, undead, etc.)
- Spells (offensive, defensive, utility)
- Abilities (active, passive, class-specific)
- NPCs (merchants, quest givers, trainers) - *Planned*
- Quests (objectives, prerequisites, rewards) - *Planned*
- Locations (dungeons, towns, wilderness) - *Planned*

### 2. Form/JSON Toggle ✅

**Dual-Mode Interface:**
- **Form Mode (Default)**: User-friendly structured editing
- **JSON Mode (Fallback)**: Raw JSON for advanced users and complex structures

**Bi-Directional Sync:**
- Form → JSON: Serialize model on mode switch
- JSON → Form: Parse and validate on mode switch
- State preservation during mode changes
- Error handling for invalid JSON

**Model Detection:**
- Automatic detection from filename patterns:
  - `*item*` → Item model
  - `*enemy*` → Enemy model
  - `*spell*` → Spell model
  - `*ability*` → Ability model
- Fallback to JSON-only for unknown types

### 3. File Browser ✅

**Features:**
- Two-panel layout (file tree + editor)
- Navigate JSON data folder structure
- File selection and loading
- Current path: `RealmEngine.Data/Data/Json` (hardcoded)

**Limitations:**
- No folder picker (hardcoded path)
- No file search/filter
- No file creation/deletion UI

---

## Planned Features (JSON Editing)

### Phase 1: Core Editing Improvements (Q1 2026)

#### 1.1 Enhanced JSON Editor 🔄
- **Monaco Editor Integration**
  - Syntax highlighting for JSON
  - Error detection and inline warnings
  - Auto-formatting and indentation
  - Bracket matching and folding
  - IntelliSense for common patterns
  
- **JSON Schema Validation**
  - Real-time validation against JSON v5.1 standards
  - Visual error markers in editor
  - Detailed error messages with suggestions
  - Validate references to other files

#### 1.2 Model Detection Improvements 🔄
- **Metadata-Based Detection**
  - Parse `type` field from JSON metadata
  - Support custom type registrations
  - Fallback to filename pattern matching
  
- **Dynamic Model Registry**
  - Auto-discover models from RealmEngine.Shared assembly
  - Plugin support for mod-added models
  - Type hints in file browser

#### 1.3 Complex Property Editing 🔄
- **Nested Object Support**
  - Recursive form generation for nested objects
  - Collapsible sections for complex hierarchies
  - Breadcrumb navigation within forms
  
- **Collection Editors**
  - List editor with add/remove/reorder
  - Dictionary editor with key-value pairs
  - Array editor with type-specific items
  - Drag-and-drop reordering

#### 1.4 Reference Browser 🔄
- **Cross-File Reference Picker**
  - Visual catalog browser for `@domain/path:item` references
  - Filter by category, rarity, level
  - Preview referenced item details
  - Validate reference exists before save
  
- **Reference Auto-Complete**
  - Type-ahead suggestions for references
  - Recently used references
  - Favorite references pinning

### Phase 2: Productivity Features (Q2 2026)

#### 2.1 Multi-File Editing 📋
- **Tabbed Interface**
  - Open multiple files simultaneously
  - Switch between tabs
  - Dirty state indicators
  - Close with unsaved changes warning

#### 2.2 Search & Filter 🔍
- **File Search**
  - Search by filename
  - Search within file content
  - Filter by file type (items, enemies, etc.)
  - Recent files list
  
- **Property Search**
  - Find items by property value
  - Bulk find-and-replace
  - Regular expression support

#### 2.3 Undo/Redo System 📝
- **Action History**
  - Undo/redo for all edits
  - Action history viewer
  - Keyboard shortcuts (Ctrl+Z, Ctrl+Y)
  - Persist history across sessions (optional)

#### 2.4 Native Folder Picker 📁
- **File System Integration**
  - MAUI IFolderPicker or platform dialogs
  - Remember last opened folder
  - Favorites/bookmarks for data folders
  - Open in system file explorer

### Phase 3: Advanced Features (Q3 2026)

#### 3.1 Batch Operations 🔧
- **Multi-File Edits**
  - Select multiple files
  - Apply property changes to all
  - Bulk rename operations
  - Mass rarity adjustments

#### 3.2 Data Templates 📄
- **Preset Templates**
  - Common item configurations (weapon, armor, potion)
  - Enemy archetypes (boss, minion, elite)
  - Quest templates (fetch, kill, explore)
  - Clone existing items with modifications

#### 3.3 Validation Suite ✓
- **Comprehensive Validation**
  - FluentValidation integration
  - Display errors inline in forms
  - Validation results panel
  - Fix suggestions for common errors
  
- **Reference Integrity**
  - Check all cross-file references exist
  - Detect circular dependencies
  - Warn on orphaned references
  - Auto-fix broken references (optional)

---

## Planned Features (Modding Support)

### Phase 4: Modding Framework (Q4 2026)

#### 4.1 Mod Project System 🎮
- **Mod Workspace**
  - Create new mod project
  - Mod metadata (name, author, version, description)
  - Dependency management (requires other mods)
  - Conflict detection (overlapping files)
  
- **Mod Structure**
  ```
  MyMod/
  ├── mod.json              # Mod manifest
  ├── Data/
  │   ├── Items/           # Custom items
  │   ├── Enemies/         # Custom enemies
  │   ├── Spells/          # Custom spells
  │   └── Abilities/       # Custom abilities
  ├── Scripts/             # Custom C# scripts (compiled)
  ├── Assets/              # Textures, audio (for Godot)
  └── README.md           # Mod documentation
  ```

#### 4.2 Mod Creation Wizard 🧙
- **Step-by-Step Wizard**
  1. Mod Info (name, author, version, description)
  2. Base Game Version (compatibility check)
  3. Dependencies (select required mods)
  4. Content Type Selection (items, enemies, quests, etc.)
  5. Initial File Generation (empty templates)
  
- **Template Selection**
  - Blank mod (empty structure)
  - Item pack (weapons, armor, consumables)
  - Enemy pack (new enemy types)
  - Quest mod (new storylines)
  - Overhaul mod (balance changes)

#### 4.3 Asset Import 📦
- **File Import System**
  - Import existing JSON files
  - Import from other mods (with permission)
  - Drag-and-drop support
  - Conflict resolution (overwrite vs merge)
  
- **Asset Library**
  - Browse installed mods
  - Copy items between mods
  - Share common assets (materials, components)

#### 4.4 Mod Validation 🔍
- **Pre-Package Validation**
  - Check all references are valid
  - Verify JSON v5.1 compliance
  - Detect conflicts with base game
  - Detect conflicts with dependencies
  - Performance impact analysis
  
- **Compatibility Checker**
  - Check against base game version
  - Check against other installed mods
  - Warn on API changes
  - Suggest fixes for compatibility issues

#### 4.5 Mod Packaging 📤
- **Export Formats**
  - `.realmmod` - Compressed mod package (ZIP with manifest)
  - Metadata included (author, version, dependencies)
  - Digital signature support (optional)
  - Steam Workshop integration (future)
  
- **Package Contents**
  - All mod files (JSON, scripts, assets)
  - README.md and LICENSE.txt
  - Changelog and version history
  - Preview images/screenshots

#### 4.6 Mod Manager 📚
- **Installed Mods**
  - List all installed mods
  - Enable/disable mods
  - Load order configuration
  - Conflict resolution
  
- **Mod Details View**
  - Mod description and metadata
  - File list and changes
  - Dependency tree
  - Update check (if online registry exists)
  
- **Load Order Management**
  - Drag-and-drop load order
  - Auto-resolve dependencies
  - Conflict warnings
  - Save/load profiles (mod lists)

### Phase 5: Advanced Modding (2027+)

#### 5.1 Script Editor 💻
- **C# Scripting Support**
  - Edit custom C# mod scripts
  - Syntax highlighting for C#
  - IntelliSense for RealmEngine API
  - Build and test scripts in-editor
  
- **Script Templates**
  - Custom item behaviors
  - Custom enemy AI
  - Custom spell effects
  - Event handlers

#### 5.2 Visual Scripting 🧩
- **Node-Based Editor**
  - No-code behavior creation
  - Drag-and-drop nodes
  - Connect inputs/outputs
  - Export to C# script
  
- **Use Cases**
  - Item triggers (on equip, on use)
  - Enemy behaviors (aggro, flee, special attack)
  - Quest logic (objectives, conditions)
  - Dialogue trees

#### 5.3 Live Testing 🎯
- **In-Editor Testing**
  - Preview mod changes in real-time
  - Test combat scenarios
  - Test item generation
  - Test quest flow
  
- **Integration with RealmEngine**
  - Launch game with mod enabled
  - Hot-reload changes while testing
  - Debug console integration
  - Performance profiling

#### 5.4 Mod Distribution 🌐
- **Mod Repository Integration**
  - Upload to mod repository
  - Version management
  - User ratings and reviews
  - Download statistics
  
- **Auto-Update System**
  - Check for mod updates
  - Download and install updates
  - Backup before update
  - Rollback if issues occur

---

## Technical Architecture

### Technology Stack

- **.NET 9.0 MAUI Blazor Hybrid** - Cross-platform desktop framework
- **Blazor Components** - HTML/CSS/Razor for UI (no XAML, no JavaScript)
- **WebView2** - Embedded browser engine (Windows)
- **RealmEngine.Shared** - Direct model integration (Item, Enemy, Spell, etc.)
- **RealmEngine.Data** - JSON data access and validation
- **System.Text.Json** - JSON serialization/deserialization
- **System.Reflection** - Dynamic form generation from models

### Architecture Pattern

**Component-Based UI:**
- Razor components for all UI elements
- Props-based data flow (parameters)
- Event callbacks for user actions
- Reactive state management (StateHasChanged)

**Models:**
- Direct use of RealmEngine.Shared models (no DTOs)
- Item, Enemy, Spell, Ability, NPC, Quest, Location
- Domain entities with business logic

**Services (Planned):**
- `ModService` - Mod project management
- `ValidationService` - Data validation
- `ReferenceResolverService` - Cross-file reference resolution
- `PackagingService` - Mod packaging and export

**Key Components:**

**Current:**
- `DynamicFormEditor.razor` - Generic form generator (~200 lines)
- `JsonEditor.razor` - Form/JSON toggle editor (~300 lines)
- `Home.razor` - Welcome screen
- `NavMenu.razor` - Navigation

**Planned:**
- `ReferencePickerDialog.razor` - Visual catalog browser
- `ModProjectExplorer.razor` - Mod file tree
- `ModWizard.razor` - Step-by-step mod creation
- `ValidationPanel.razor` - Error/warning display
- `SearchPanel.razor` - File/content search
- `BatchEditor.razor` - Multi-file operations

---

## Usage Workflow

### Current: JSON Editing

1. **Launch RealmForge**
   - Open the Blazor Hybrid application
   - Application shows Home screen

2. **Navigate to Editor**
   - Click "JSON Editor" in navigation menu
   - File browser appears (left panel)

3. **Open File**
   - Browse to JSON data file (e.g., `items/weapons/swords/catalog.json`)
   - File loads with automatic model detection
   - Form mode displays (default)

4. **Edit in Form Mode**
   - Edit fields in auto-generated form
   - String inputs for names/descriptions
   - Number inputs for stats/values
   - Dropdowns for enums (rarity, type, etc.)
   - Checkboxes for boolean flags

5. **Switch to JSON Mode (Optional)**
   - Toggle to JSON mode to see raw data
   - Edit JSON directly if needed
   - Switch back to Form mode

6. **Save Changes**
   - Click Save button
   - Validation runs automatically
   - File written to disk
   - Success notification

### Planned: Mod Creation

1. **Create Mod Project**
   - Click "New Mod" in menu
   - Enter mod metadata (name, author, version)
   - Select base game version
   - Choose template (item pack, enemy pack, quest mod)

2. **Add Content**
   - Create new items/enemies/spells
   - Use templates or clone existing
   - Edit in form mode
   - Test references

3. **Configure Mod**
   - Set dependencies (required mods)
   - Configure load order
   - Add README and screenshots

4. **Validate Mod**
   - Run validation suite
   - Check reference integrity
   - Test compatibility
   - Review warnings

5. **Package Mod**
   - Export as `.realmmod` file
   - Include all assets and metadata
   - Generate checksums
   - Ready for distribution

6. **Distribute**
   - Upload to mod repository (future)
   - Share .realmmod file directly
   - Users install via Mod Manager

---

## Data Validation

### Current Validation

- **JSON Parsing**: Validates JSON syntax on load
- **Type Safety**: Form inputs enforce correct types (string, int, bool, enum)
- **Model Validation**: C# models validate data structure

### Planned Validation

- **JSON v5.1 Compliance**: Validate against RealmEngine standards
- **FluentValidation Integration**: Display inline error messages
- **Reference Validation**: Check cross-file references exist
- **Circular Dependency Detection**: Prevent infinite reference loops
- **Value Range Validation**: Ensure stats within valid ranges
- **Required Field Validation**: Enforce mandatory properties
- **Duplicate Detection**: Warn on duplicate IDs/names

---

## Error Prevention

### Current Safety Features

- **Type-Safe Forms**: Inputs enforce correct data types
- **Model-Driven**: Forms generated from actual models (no schema drift)
- **Bi-Directional Sync**: Serialization preserves data structure
- **Read-Only Complex Types**: Prevents breaking nested objects

### Planned Safety Features

- **Auto-Backup**: Backup before every save
- **Atomic Saves**: All-or-nothing file writes
- **Validation Before Save**: Block invalid data from being written
- **Undo/Redo**: Revert accidental changes
- **Conflict Detection**: Warn when editing files modified externally
- **Reference Existence Check**: Prevent broken references

---

## Known Limitations (Current v3.0)

1. **Hardcoded Data Path** - Currently points to `RealmEngine.Data/Data/Json`
2. **Simple Model Detection** - Based on filename string matching only
3. **Complex Types Not Editable** - Lists, Dictionaries, nested objects are read-only
4. **Basic JSON Editor** - Plain textarea (no syntax highlighting)
5. **No Validation Feedback** - Model validation errors not displayed
6. **No Folder Picker** - Cannot browse to custom data folders
7. **No File Search** - Must manually browse folder tree
8. **No Undo/Redo** - Cannot revert changes
9. **Single File Editing** - Cannot edit multiple files simultaneously
10. **No Mod Support** - Modding features not yet implemented

---

## Roadmap

### Q1 2026: Core Editing (v3.1)
- ✅ Dynamic Form Editor
- ✅ Form/JSON Toggle
- ✅ Model Detection
- ✅ File Browser
- 🔄 Monaco Editor Integration
- 🔄 FluentValidation Display
- 🔄 Complex Property Editing
- 🔄 Reference Browser

### Q2 2026: Productivity (v3.2)
- Multi-file tabbed editing
- Search and filter
- Undo/redo system
- Native folder picker
- Batch operations
- Data templates

### Q3 2026: Advanced Editing (v3.3)
- Enhanced validation suite
- Reference integrity checker
- Visual dependency graphs
- Performance optimization
- Dark mode theme

### Q4 2026: Modding Framework (v4.0)
- Mod project system
- Mod creation wizard
- Asset import
- Mod validation
- Mod packaging
- Mod manager

### 2027+: Advanced Modding (v5.0+)
- C# script editor
- Visual scripting (node-based)
- Live testing integration
- Mod distribution platform
- Auto-update system
- Workshop integration

---

## Installation & Setup

### Requirements

- Windows 10/11 (64-bit) - *Primary target*
- macOS 10.15+ - *Planned*
- Linux (Ubuntu 20.04+, Fedora 36+) - *Planned*
- .NET 9.0 Runtime
- 150 MB disk space
- 2 GB RAM minimum
- 4 GB RAM recommended (for large mods)

### Installation (Current)

**Development Build:**
1. Clone RealmEngine repository
2. Navigate to `RealmForge/` folder
3. Run: `dotnet run --project RealmForge.csproj -f net9.0-windows10.0.19041.0`

**Future Release Build:**
1. Download RealmForge installer from releases
2. Run installer (will include .NET runtime)
3. Launch RealmForge from Start Menu
4. Configure data folder path in settings

### Configuration (Planned)

**On First Launch:**
- Set RealmEngine data folder path
- Configure editor preferences
  - Default mode (Form vs JSON)
  - Auto-save interval
  - Backup settings
- Set mod workspace folder
- Choose theme (light/dark)

---

## Testing

### Current Tests

**RealmEngine Integration:**
- All RealmEngine.Shared models compile with RealmForge
- JSON serialization/deserialization works correctly
- Form generation from models successful

### Planned Tests

**Unit Tests:**
- Component rendering (DynamicFormEditor)
- Model detection logic
- Serialization/deserialization
- Validation rules
- File I/O operations

**Integration Tests:**
- End-to-end editing workflow
- Form ↔ JSON sync
- Cross-file reference resolution
- Mod packaging
- Multi-file operations

**UI Tests:**
- Form input behavior
- Navigation between pages
- File browser operations
- Save/load workflows

---

## Performance Considerations

### Current Performance

- **Form Generation**: Fast for simple models (<100 properties)
- **File Loading**: Instant for files <1MB
- **Serialization**: Negligible overhead

### Planned Optimizations

- **Large File Handling**: Streaming JSON parser for files >10MB
- **Virtual Scrolling**: For long property lists (>100 items)
- **Lazy Loading**: Load file tree on-demand
- **Caching**: Cache parsed models to avoid re-parsing
- **Background Validation**: Run validation in background thread
- **Diff-Based Saves**: Only write changed properties

---

## Accessibility

### Planned Features

- **Keyboard Navigation**: Full keyboard support for all operations
- **Screen Reader Support**: ARIA labels and descriptions
- **High Contrast Mode**: Support for Windows high contrast themes
- **Configurable Font Size**: Adjust editor font size
- **Color Blind Mode**: Alternative color schemes
- **Focus Indicators**: Clear visual focus indicators

---

## Localization (Future)

### Supported Languages (Planned)

- English (en-US) - Default
- Spanish (es-ES)
- French (fr-FR)
- German (de-DE)
- Japanese (ja-JP)
- Chinese Simplified (zh-CN)

### Translation System

- Resource files for all UI strings
- Community-driven translations
- In-game language switcher
- RTL language support (Arabic, Hebrew)

---

## Support & Community

### Documentation

- **This File**: `docs/RealmForge/README.md` - Feature documentation
- **RealmForge Project**: `RealmForge/README.md` - Technical documentation
- **JSON Standards**: `docs/standards/json/` - Data format specifications
- **API Documentation**: XML comments in RealmEngine.Shared

### Getting Help

- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: Questions and community support
- **Discord**: Real-time chat and modding help (future)
- **Wiki**: Tutorials and guides (future)

### Contributing

**Code Contributions:**
- Fork repository
- Create feature branch
- Submit pull request
- Follow code style guidelines

**Mod Contributions:**
- Share mods via GitHub
- Submit to mod repository (future)
- Join modding community

---

## License

RealmForge is part of the RealmEngine project.  
See main repository LICENSE file for details.

---

## Version History

- **v3.0** (January 12, 2026): Complete rewrite in Blazor Hybrid, dynamic form editor, modding roadmap
- **v2.0** (January 5, 2026): Renamed from ContentBuilder, JSON v4.1 support, WPF implementation
- **v1.0** (December 2025): Initial WPF release with pattern editor

---

## Credits

**Development:**
- Primary Developer: [Your Name]
- Framework: .NET MAUI Blazor Hybrid
- Models: RealmEngine.Shared

**Special Thanks:**
- Microsoft .NET team for MAUI Blazor
- RealmEngine community for feedback and testing
