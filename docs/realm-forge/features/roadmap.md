# RealmForge Roadmap

**Last Updated**: January 12, 2026

---

## Version 3.0 (Current) - Foundation

**Status**: ✅ Complete

### Delivered Features
- Dynamic form editor with reflection-based generation
- Form/JSON mode toggle with bi-directional sync
- File browser for JSON data navigation
- Model detection (Item, Enemy, Spell, Ability)
- Type-specific inputs (string, int, double, bool, enum)
- Save/load functionality

---

## Version 3.1 - Enhanced Editing & Validation

**Status**: 📋 Planned

### Priority 1: Core Libraries & Infrastructure
- [ ] **MudBlazor Integration** - Replace Bootstrap with Material Design components
- [ ] **Monaco Editor** - Replace textarea with professional code editor
- [ ] **FluentValidation** - Integrate validation with inline error display
- [ ] **Serilog** - Add structured logging throughout app
- [ ] **bUnit** - Set up component testing framework
- [ ] **Blazored.LocalStorage** - Settings persistence and auto-save

### Priority 2: UI Components (MudBlazor)
- [ ] **MudTreeView** - File browser with expand/collapse
- [ ] **MudDataGrid** - Display items in reference picker
- [ ] **MudDialog** - Modal dialogs for pickers and wizards
- [ ] **MudAutocomplete** - Reference search with suggestions
- [ ] **MudThemeProvider** - Dark/light mode support
- [ ] **MudAppBar/Drawer** - Improved navigation layout

### Priority 3: Reference Browser (HIGH PRIORITY)
- [ ] Visual catalog browser for `@domain/path:item` references
- [ ] Filter by category, rarity, level
- [ ] Preview referenced item details
- [ ] Search functionality
- [ ] Validate reference exists before save
- [ ] Reference auto-complete in forms

### Priority 4: Complex Property Editing
- [ ] List editor (add/remove/reorder items)
- [ ] Dictionary editor (key-value pairs)
- [ ] Nested object editor (recursive forms)
- [ ] Support for RealmEngine.Shared model structures

### Priority 5: Auto-Save & Settings
- [ ] Auto-save with configurable interval
- [ ] Settings persistence (theme, layout, paths)
- [ ] Recent files tracking
- [ ] Editor state preservation
- [ ] Backup before manual save

### Priority 6: Enhanced Model Detection
- [ ] Metadata-based detection from JSON `type` field
- [ ] Dynamic model registry from RealmEngine.Shared
- [ ] Support for custom/plugin models
- [ ] Type hints in file browser

---

## Version 3.2 - Productivity

**Status**: 📋 Planned

### Multi-File Support
- [ ] Tabbed interface for multiple files
- [ ] Dirty state indicators
- [ ] Unsaved changes warnings

### Search & Navigation
- [ ] File search by name
- [ ] Content search within files
- [ ] Filter by file type
- [ ] Recent files list
- [ ] Favorites/bookmarks

### History & Recovery
- [ ] Undo/redo system
- [ ] Action history viewer
- [ ] Auto-backup before saves
- [ ] Recover unsaved changes

### Batch Operations
- [ ] Multi-file selection
- [ ] Bulk property updates
- [ ] Mass rarity adjustments
- [ ] Batch rename

---

## Version 3.3 - Advanced Validation

**Status**: 📋 Planned

### Validation Suite
- [ ] JSON v5.1 compliance checker
- [ ] Reference integrity validation
- [ ] Circular dependency detection
- [ ] Value range validation
- [ ] Required field enforcement

### Visual Tools
- [ ] Dependency graph visualization
- [ ] Reference explorer
- [ ] Validation results panel
- [ ] Quick-fix suggestions

### Templates & Presets
- [ ] Item templates (weapon, armor, consumable)
- [ ] Enemy archetypes (boss, minion, elite)
- [ ] Quest templates
- [ ] Clone with modifications

---

## Version 4.0 - Modding Framework

**Status**: 📋 Planned (Deferred)

### Mod Project System
- [ ] Create new mod projects
- [ ] Mod metadata editor
- [ ] Dependency management
- [ ] Load order configuration

### Mod Creation
- [ ] Step-by-step mod wizard
- [ ] Template selection (item pack, enemy pack, quest mod)
- [ ] Asset import system
- [ ] File organization

### Mod Validation
- [ ] Pre-package validation
- [ ] Compatibility checker
- [ ] Conflict detection
- [ ] Performance analysis

### Mod Distribution
- [ ] Export as `.realmmod` package
- [ ] Package compression
- [ ] Metadata inclusion
- [ ] Checksum generation

### Mod Manager
- [ ] Installed mods list
- [ ] Enable/disable mods
- [ ] Load order management
- [ ] Mod details view

---

## Version 5.0+ - Advanced Modding

**Status**: 🔮 Future

### Scripting Support
- [ ] C# script editor with syntax highlighting
- [ ] IntelliSense for RealmEngine API
- [ ] Build and test scripts
- [ ] Script templates

### Visual Scripting
- [ ] Node-based behavior editor
- [ ] Drag-and-drop logic
- [ ] Export to C# scripts
- [ ] No-code item behaviors

### Live Testing
- [ ] In-editor preview
- [ ] Integration with RealmEngine
- [ ] Hot-reload changes
- [ ] Debug console

### Distribution Platform
- [ ] Mod repository integration
- [ ] Upload and publish
- [ ] Version management
- [ ] Auto-update system
- [ ] Community ratings

---

## Feature Priority Matrix

### High Priority (v3.1-3.2)
1. Monaco Editor integration
2. Complex property editing
3. Reference browser
4. Multi-file tabs
5. Undo/redo

### Medium Priority (v3.3)
6. Validation suite
7. Templates system
8. Batch operations
9. Search functionality
10. Dependency graphs

### Low Priority (v4.0+)
11. Mod project system
12. Mod packaging
13. Mod manager
14. Script editor
15. Visual scripting

---

## Technology Upgrades

### .NET Updates
- Monitor .NET 10 preview features
- Evaluate WebAssembly performance improvements
- Consider Blazor United architecture

### UI Enhancements
- Explore Fluent Design System
- Investigate Material Design 3
- Evaluate native UI components vs Blazor

### Performance
- Profile large file loading
- Optimize reflection-based form generation
- Implement virtual scrolling for long lists
- Cache parsed models

---

## Community Feedback Integration

### Requested Features (Under Review)
- Drag-and-drop file organization
- Export to Excel/CSV for bulk editing
- Import from spreadsheet
- Real-time collaboration (multi-user)
- Version control integration
- Plugin system for custom editors

---

## Breaking Changes

### Version 3.x → 4.0
- Mod file structure changes
- New `.realmmod` package format
- Deprecation of direct JSON editing for mod files
- Migration tool provided

### Version 4.x → 5.0
- Script API changes
- New scripting system
- Legacy mod compatibility mode

---

## Success Metrics

### v3.1 Goals
- 90%+ user satisfaction with Monaco Editor
- <2 second load time for files <1MB
- Zero data loss incidents

### v3.2 Goals
- 80% of users use multi-file tabs
- Undo/redo used in 60% of editing sessions
- 50% reduction in file navigation time

### v4.0 Goals
- 100+ community mods created
- 5,000+ mod downloads
- 95% mod validation pass rate

---

## Support & Resources

**Development:**
- Project: `RealmForge/`
- Tests: `RealmForge.Tests/` (planned)
- Documentation: `docs/realm-forge/`

**Community:**
- GitHub Issues for bug reports
- GitHub Discussions for feature requests
- Discord (future) for real-time help
