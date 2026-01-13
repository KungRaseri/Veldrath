# RealmForge Implementation Order

**Last Updated**: January 12, 2026

This document outlines the agreed-upon implementation order for RealmForge v3.1 features.

---

## Phase 1: Core Libraries & Infrastructure

**Goal**: Set up foundational dependencies and tooling

### 1.1 NuGet Package Installation
- [x] MudBlazor (v8.0.0 - auto-resolved from 7.24.0)
- [x] BlazorMonaco (v3.3.0)
- [x] FluentValidation (v12.1.1)
- [x] Serilog (v4.3.0)
  - [x] Serilog.Sinks.File (v7.0.0)
  - [x] Serilog.Sinks.Console (v6.1.1)
  - [x] Serilog.Sinks.Debug (v3.0.0)
  - [x] Serilog.Extensions.Logging (v9.0.1)
- [x] bUnit (v1.32.7)
- [x] Blazored.LocalStorage (v4.5.0)

### 1.2 Service Registration
- [x] Configure DI in `MauiProgram.cs`
- [x] Register Serilog with configuration using `LoggingSettings` from RealmEngine.Core
- [x] Register MudBlazor services (AddMudServices)
- [x] Register Blazored.LocalStorage
- [x] Add RealmEngine.Core project reference
- [x] Integrate RealmEngine.Core logging standards
- [x] Create EditorSettingsService (settings persistence)
- [x] Create FileManagementService (load/save JSON files)
- [x] Create ModelValidationService (Phase 4 stub)
- [x] Create ReferenceResolverService (Phase 5 stub)
- [x] Register all application services in DI

### 1.3 Testing Setup
- [x] Create RealmForge.Tests project
- [x] Configure bUnit test context
- [x] Add FluentAssertions for readable assertions
- [x] Create infrastructure smoke tests (3 tests passing)

### 1.4 MudBlazor Integration
- [x] Add MudBlazor CSS/JS to index.html
- [x] Add MudThemeProvider to Routes.razor
- [x] Add MudDialogProvider, MudSnackbarProvider, MudPopoverProvider
- [x] Add @using MudBlazor to _Imports.razor

**Deliverable**: ✅ **Phase 1 COMPLETE!** All dependencies installed, services registered and ready, tests passing (3/3)

---

## Phase 2: MudBlazor UI Components

**Goal**: Replace basic HTML with Material Design components

### 2.1 Theme & Layout
- [x] Update MainLayout.razor to use MudLayout
- [x] Add MudAppBar for top navigation
- [x] Replace NavMenu with MudDrawer and MudNavMenu
- [x] Add theme toggle button (light/dark)
- [x] Implement theme persistence with EditorSettingsService
- [x] Add drawer toggle functionality

### 2.2 File Browser (FileTreeView)
- [x] Create FileTreeNode model for hierarchical display
- [x] Build file tree from directory structure
- [x] Create FileTreeView.razor component with custom rendering
- [x] Add file type icons (JSON, folders)
- [x] Add expand/collapse functionality
- [x] Wire up file selection to JsonEditor
- [x] Use RealmEngine.Shared/Data/Json as default path

### 2.3 JsonEditor MudBlazor Updates
- [x] Convert JsonEditor to use MudGrid layout
- [x] Replace toolbar with MudButtons and MudToggleGroup
- [x] Add MudTextField for JSON content (Monaco in Phase 3)
- [x] Implement file load/save with services
- [x] Add validation feedback with MudAlert
- [x] Empty state with centered message

**Deliverable**: ✅ **Phase 2 COMPLETE!** Material Design UI throughout app, functional file tree, modern editor interface

---

## Phase 3: Monaco Editor Integration

**Goal**: Professional JSON editing experience

### 3.1 Monaco Component
- [ ] Create MonacoEditor.razor wrapper component
- [ ] Configure JSON language support
- [ ] Add syntax highlighting
- [ ] Add error detection
- [ ] Configure themes (match app theme)
- [ ] Add keyboard shortcuts

### 3.2 JsonEditor Integration
- [ ] Replace textarea with MonacoEditor in JSON mode
- [ ] Maintain bi-directional sync (Form ↔ Monaco)
- [ ] Add loading indicator for large files
- [ ] Test serialization/deserialization
- [ ] Write bUnit tests for Monaco integration

**Deliverable**: Monaco Editor working in JSON mode

---

## Phase 4: FluentValidation Integration

**Goal**: Display validation errors in forms

### 4.1 Validators
- [ ] Create validators for Item, Enemy, Spell, Ability models
- [ ] Define validation rules (required, range, format)
- [ ] Test validators with unit tests

### 4.2 Validation Service
- [ ] Create ValidationService
- [ ] Implement ValidateAsync method
- [ ] Return ValidationResult with errors
- [ ] Write unit tests for service

### 4.3 UI Integration
- [ ] Create ValidationPanel component (displays errors)
- [ ] Add inline error messages in DynamicFormEditor
- [ ] Highlight invalid fields with red border
- [ ] Show validation summary on save
- [ ] Write bUnit tests for validation display

**Deliverable**: Real-time validation feedback in forms

---

## Phase 5: Reference Browser (HIGH PRIORITY)

**Goal**: Visual picker for JSON v5.1 references

### 5.1 Reference Models
- [ ] Create ReferenceInfo model (domain, path, item)
- [ ] Parse reference syntax: `@domain/path:item`
- [ ] Create ReferenceCategory model

### 5.2 Reference Resolver Service
- [ ] Create ReferenceResolverService
- [ ] Scan data folder for all referenceable items
- [ ] Build reference catalog in memory
- [ ] Implement search/filter logic
- [ ] Cache results for performance
- [ ] Write unit tests

### 5.3 Reference Picker Dialog
- [ ] Create ReferencePickerDialog.razor (MudDialog)
- [ ] Display items in MudDataGrid
- [ ] Add search with MudAutocomplete
- [ ] Add filters (category, rarity, level)
- [ ] Show item preview on hover
- [ ] Return selected reference
- [ ] Write bUnit tests

### 5.4 Form Integration
- [ ] Detect reference properties (string with `@` prefix)
- [ ] Add "Browse..." button next to reference fields
- [ ] Open ReferencePickerDialog on click
- [ ] Display selected reference name
- [ ] Validate reference exists on save
- [ ] Write integration tests

**Deliverable**: Working reference browser for `@domain/path:item` syntax

---

## Phase 6: Complex Property Editing

**Goal**: Edit lists, dictionaries, and nested objects

### 6.1 List Editor
- [ ] Detect List<T> properties
- [ ] Create ListEditor component
- [ ] Add/remove items
- [ ] Reorder items (drag-drop with MudDropZone)
- [ ] Edit items inline or in dialog
- [ ] Write bUnit tests

### 6.2 Dictionary Editor
- [ ] Detect Dictionary<K,V> properties
- [ ] Create DictionaryEditor component
- [ ] Add/remove key-value pairs
- [ ] Validate key uniqueness
- [ ] Type-specific value inputs
- [ ] Write bUnit tests

### 6.3 Nested Object Editor
- [ ] Detect nested object properties
- [ ] Recursive form generation
- [ ] Collapsible sections (MudExpansionPanel)
- [ ] Breadcrumb navigation
- [ ] Write bUnit tests

### 6.4 DynamicFormEditor Updates
- [ ] Integrate complex property editors
- [ ] Show appropriate editor based on type
- [ ] Handle null values
- [ ] Update tests

**Deliverable**: Full editing support for RealmEngine.Shared models

---

## Phase 7: Auto-Save & Settings

**Goal**: Persistence and user preferences

### 7.1 Settings Service
- [ ] Create SettingsService
- [ ] Define EditorSettings model (theme, paths, intervals)
- [ ] Load settings from LocalStorage on startup
- [ ] Save settings on change
- [ ] Write unit tests

### 7.2 Auto-Save
- [ ] Create AutoSaveService
- [ ] Configurable interval (default 2 minutes)
- [ ] Track dirty state per file
- [ ] Save to LocalStorage (temp)
- [ ] Notification on auto-save
- [ ] Write unit tests

### 7.3 Settings Page
- [ ] Create Settings.razor page
- [ ] Form for editor preferences
- [ ] Theme selector (light/dark)
- [ ] Auto-save interval slider
- [ ] Data folder path input
- [ ] Save settings button

### 7.4 Recent Files
- [ ] Track recently opened files
- [ ] Store in LocalStorage
- [ ] Display in Home page
- [ ] Quick open from recent list

**Deliverable**: Auto-save working, settings persisted

---

## Phase 8: Enhanced Model Detection

**Goal**: Better model type detection

### 8.1 Metadata-Based Detection
- [ ] Parse JSON `type` field from metadata
- [ ] Map type strings to C# models
- [ ] Fallback to filename pattern matching
- [ ] Write unit tests

### 8.2 Dynamic Model Registry
- [ ] Scan RealmEngine.Shared assembly for models
- [ ] Register all IGameData implementations
- [ ] Support for plugin models (future)
- [ ] Write unit tests

### 8.3 UI Updates
- [ ] Show model type in file browser (icon/badge)
- [ ] Type hints on hover
- [ ] Filter files by model type

**Deliverable**: Robust model detection system

---

## Phase 9: Polish & Testing

**Goal**: Production-ready quality

### 9.1 Comprehensive Testing
- [ ] Achieve 80%+ code coverage
- [ ] Integration tests for full workflows
- [ ] Performance tests for large files
- [ ] UI tests for all components

### 9.2 Error Handling
- [ ] Global error boundary
- [ ] User-friendly error messages
- [ ] Error logging with Serilog
- [ ] Recovery options

### 9.3 Performance
- [ ] Profile file loading
- [ ] Optimize reflection-based form generation
- [ ] Implement caching where needed
- [ ] Virtual scrolling for large lists

### 9.4 Documentation
- [ ] Update all feature docs
- [ ] Add inline code comments
- [ ] Create user guide
- [ ] Record demo video (optional)

**Deliverable**: Stable, tested v3.1 release

---

## Success Criteria (v3.1)

- ✅ All Phase 1-8 features implemented
- ✅ 80%+ test coverage with bUnit
- ✅ Monaco Editor fully functional
- ✅ Reference browser working for JSON v5.1
- ✅ Complex properties editable (lists, dicts, nested)
- ✅ Auto-save preventing data loss
- ✅ FluentValidation integrated with UI
- ✅ No critical bugs
- ✅ Load files <1MB in <2 seconds

---

## After v3.1: v3.2 Preview

Once v3.1 is stable, we'll tackle:
- Multi-file tabbed editing
- Search & filter
- Undo/redo system
- Batch operations
- Native folder picker

See [Roadmap](roadmap.md) for full v3.2 features.
