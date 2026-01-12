# JSON Editing Features

**Last Updated**: January 12, 2026

---

## Overview

RealmForge provides dual-mode JSON editing: form-based structured editing for common tasks and raw JSON editing for advanced scenarios. All JSON files conform to RealmEngine JSON v5.1 standards.

---

## Current Features (v3.0)

### Form/JSON Mode Toggle ✅

**Form Mode (Default):**
- Auto-generated forms from RealmEngine.Shared models
- Type-safe inputs prevent syntax errors
- Field labels with proper formatting
- Validation on input (basic)
- No JSON knowledge required

**JSON Mode (Fallback):**
- Plain textarea for raw JSON editing
- Manual editing for complex structures
- Useful when form doesn't support nested data
- Direct access to all JSON properties

**Mode Switching:**
- Radio button toggle
- Form → JSON: Serialize model with `System.Text.Json`
- JSON → Form: Deserialize and validate
- State preserved during switch
- Error handling for invalid JSON

### Model Detection ✅

**Automatic Detection:**
- Based on filename patterns:
  - `*item*` → `RealmEngine.Shared.Models.Item`
  - `*enemy*` → `RealmEngine.Shared.Models.Enemy`
  - `*spell*` → `RealmEngine.Shared.Models.Spell`
  - `*ability*` → `RealmEngine.Shared.Models.Ability`
- Fallback to JSON-only for unknown types

**Limitations:**
- Simple string matching (not robust)
- No support for custom types yet
- Cannot detect from JSON metadata

### File Browser ✅

**Features:**
- Two-panel layout (tree + editor)
- Navigate folder hierarchy
- File selection and loading
- Display current file path

**Limitations:**
- Hardcoded path: `RealmEngine.Data/Data/Json`
- No folder picker dialog
- No search or filter
- No file creation/deletion

### Save/Load Operations ✅

**Load:**
- Read JSON from file system
- Deserialize to model (if supported)
- Display in form or JSON mode
- Error handling for corrupt files

**Save:**
- Serialize model to JSON
- Write to file system
- Overwrite existing file
- Basic error handling

**Limitations:**
- No backup before save
- No auto-save
- No conflict detection (external edits)
- No atomic writes

---

## Planned Features

### Monaco Editor Integration (v3.1)

**Features:**
- Syntax highlighting for JSON
- Error detection and squiggly underlines
- Bracket matching and auto-closing
- Code folding for nested structures
- Auto-indentation
- IntelliSense for JSON structure
- Find/replace with regex
- Multi-cursor editing

**Benefits:**
- Professional code editing experience
- Faster manual JSON editing
- Fewer syntax errors
- Better readability

**Integration:**
- Replace plain textarea in JSON mode
- Embed via BlazorMonaco NuGet package
- Configure JSON language support
- Custom themes (light/dark)

### Enhanced Model Detection (v3.1)

**Metadata-Based Detection:**
```json
{
  "version": "5.1",
  "type": "item_catalog",
  "description": "Weapon definitions"
}
```
- Parse `type` field to determine model
- More reliable than filename matching
- Support for custom types

**Dynamic Model Registry:**
- Scan RealmEngine.Shared assembly for models
- Register all `IGameData` implementations
- Support plugin-added models
- Type hints in file browser UI

### Complex Property Editing (v3.1)

**Nested Objects:**
```csharp
public class Item {
    public Stats BaseStats { get; set; }  // Nested object
}
```
- Recursive form generation
- Collapsible sections
- Breadcrumb navigation
- Edit in-place or in modal dialog

**Lists/Arrays:**
```csharp
public class Enemy {
    public List<Ability> Abilities { get; set; }  // List
}
```
- Add/remove items
- Reorder with drag-and-drop
- Edit item inline or in dialog
- Validation for list constraints

**Dictionaries:**
```csharp
public class Material {
    public Dictionary<string, int> Bonuses { get; set; }  // Dictionary
}
```
- Key-value pair editor
- Add/remove entries
- Validate key uniqueness
- Type-specific value inputs

### Reference Browser (v3.1)

**Visual Catalog Picker:**
- Browse available references by category
- Filter by rarity, level, type
- Preview referenced item details
- Search functionality
- Recently used references

**Reference Syntax:**
```json
"material": "@items/materials/metals:iron",
"ability": "@abilities/active/offensive:fireball",
"enemy": "@enemies/humanoid:goblin-warrior"
```

**Features:**
- Click reference field to open picker
- Visual cards for each item
- Detailed tooltips
- Validate reference exists before save
- Auto-complete while typing

**Validation:**
- Check reference target exists
- Validate domain and path
- Warn on missing references
- Suggest similar references

### Multi-File Editing (v3.2)

**Tabbed Interface:**
- Open multiple files simultaneously
- Switch between tabs
- Tab context menu (close, close others, close all)
- Dirty state indicator (unsaved changes)
- Tab icons by file type

**Session Management:**
- Remember open tabs on close
- Restore session on reopen
- Save tab layout
- Named sessions (profiles)

**Benefits:**
- Edit related files together
- Copy/paste between files
- Compare files side-by-side
- Faster workflow

### Search & Filter (v3.2)

**File Search:**
- Search by filename (fuzzy matching)
- Filter by file type (item, enemy, spell)
- Filter by folder location
- Recent files list (MRU)
- Favorites/bookmarks

**Content Search:**
- Search within file content
- Find all files containing text
- Regular expression support
- Search results panel
- Go to result (opens file + highlights)

**Property Search:**
- Find items by property value
- Example: "Find all items with rarity=Legendary"
- Advanced filters (range, exists, type)
- Export search results

### Undo/Redo System (v3.2)

**Action History:**
- Track all property changes
- Undo/redo with Ctrl+Z/Ctrl+Y
- Action history viewer
- Named checkpoints
- Persistent history (optional)

**Supported Actions:**
- Property edits
- Add/remove list items
- File operations (create, delete, rename)
- Mode switches (form ↔ JSON)

**UI:**
- Undo/redo buttons in toolbar
- History panel with action list
- Timestamp for each action
- Jump to any point in history

### Native Folder Picker (v3.2)

**File System Integration:**
- MAUI `IFolderPicker` or platform dialogs
- Browse to any folder
- Remember last opened folder
- Favorites/bookmarks for common locations
- Open in system file explorer

**Configuration:**
- Store folder path in settings
- Multiple workspace paths
- Per-project data folders

---

## JSON v5.1 Standards

### Compliance

All JSON files must conform to:
- **catalog.json**: Item/enemy definitions with `version`, `type`, `description`
- **names.json**: Pattern generation with `patterns[]`, `components{}`
- **.cbconfig.json**: ContentBuilder metadata with `icon`, `sortOrder`

See `docs/standards/json/` for detailed specifications.

### Reference System

**Syntax:** `@domain/path/category:item-name[filters]?.property`

**Examples:**
```json
"weapon": "@items/weapons/swords:iron-longsword",
"spell": "@spells/offensive:fireball",
"material": "@items/materials/metals:iron.hardness"
```

**Features:**
- Direct item references
- Property access with dot notation
- Wildcard selection `:*` for random
- Optional references with `?` suffix
- Filtering support

See `docs/standards/json/JSON_REFERENCE_STANDARDS.md` for details.

---

## Validation

### Current Validation
- JSON syntax validation on parse
- Type safety from C# models
- Basic required field checks

### Planned Validation (v3.3)
- JSON v5.1 schema compliance
- Reference existence checks
- Circular dependency detection
- Value range validation
- Custom FluentValidation rules
- Pre-save validation gate

---

## Performance Considerations

### Current Performance
- Fast for files <100KB
- Instant form generation for simple models
- No optimization for large files

### Planned Optimizations
- Streaming parser for files >10MB
- Virtual scrolling for long property lists
- Lazy loading for file tree
- Background validation
- Model caching
- Diff-based saves (only changed properties)

---

## Error Handling

### Current
- Basic try/catch on load/save
- Console error logging
- Generic error messages

### Planned
- Detailed error messages with suggestions
- Visual error indicators in forms
- Validation error panel
- Recovery options (retry, skip, cancel)
- Error logging to file
- Submit error reports

---

## Accessibility

### Planned Features
- Full keyboard navigation
- Screen reader support (ARIA labels)
- High contrast mode
- Configurable font sizes
- Focus indicators
- Tab order optimization

---

## Future Enhancements

### Advanced Editing (v3.3+)
- Inline diff viewer for changes
- Compare two files side-by-side
- Merge changes from another file
- Batch find/replace across files
- Macros for repetitive tasks

### Collaboration (v5.0+)
- Real-time multi-user editing
- Change tracking with attribution
- Conflict resolution UI
- Comment on properties
- Review/approval workflow

### Version Control (v5.0+)
- Git integration
- Commit from editor
- View file history
- Revert to previous version
- Branch management
