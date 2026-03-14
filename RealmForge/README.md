# RealmForge - Blazor Hybrid JSON Editor

**Status**: ✅ Functional - Form-based editing with JSON fallback  
**Technology**: .NET MAUI Blazor Hybrid  
**Target**: Windows Desktop (cross-platform ready)

## Overview

RealmForge is a desktop application for editing RealmEngine's JSON data files. Built with Blazor Hybrid, it provides a dynamic form-based editor backed by RealmEngine.Shared models, with a JSON fallback mode for advanced editing.

## Features

### ✅ Implemented
- **Dynamic Form Editor** - Auto-generated forms from C# models using reflection
- **Form/JSON Toggle** - Switch between form editing (default) and raw JSON
- **Model Detection** - Automatically detects Item, Enemy, Spell, Ability from filename
- **Type-Specific Inputs** - String, int, double, bool, enum fields with proper controls
- **Bi-Directional Sync** - Form ↔ JSON serialization
- **Save/Load** - Persist changes to files
- **Basic Validation** - JSON parsing validation

### 🚧 Planned
- **Monaco Editor** - Syntax highlighting for JSON mode
- **FluentValidation Integration** - Display model validation errors in form
- **Complex Property Editing** - Recursive forms or inline JSON for lists/nested objects
- **Folder Picker** - Native file system dialog (currently hardcoded path)
- **Better Model Detection** - Parse JSON metadata instead of filename matching
- **Undo/Redo** - Change history
- **Search and Filter** - Find files by name or content
- **Dark Mode** - Theme support

## Running the App

### Development
```powershell
dotnet run --project RealmForge/RealmForge.csproj -f net9.0-windows10.0.19041.0
```

### Build
```powershell
dotnet build RealmForge/RealmForge.csproj -f net9.0-windows10.0.19041.0
```

### Build Release
```powershell
dotnet publish -f net9.0-windows10.0.19041.0 -c Release
```

## Usage

1. Launch RealmForge
2. Click "JSON Editor" in navigation
3. Browse to a JSON file (e.g., `items/weapons/swords/catalog.json`)
4. **Form Mode (Default)**: Edit fields in generated form
5. **JSON Mode**: Switch to raw JSON editing
6. Save changes

## Component Architecture

### DynamicFormEditor.razor (~200 lines)

Generic form generator for any C# model:

```razor
<DynamicFormEditor TModel="Item" 
                  Model="@itemModel" 
                  OnSave="@HandleSave" 
                  OnCancel="@HandleCancel" />
```

**Features:**
- Reflection-based property inspection
- Type-specific controls (InputText, InputNumber, InputCheckbox, select)
- Automatic label generation (PascalCase → Title Case)
- Complex type detection (read-only display)
- EventCallback for save/cancel actions

**Supported Property Types:**
- `string` → `<InputText>`
- `int` → `<InputNumber>`
- `double`/`float` → `<InputNumber>`
- `bool` → `<InputCheckbox>`
- `enum` → `<select>` dropdown
- Complex types → Read-only message

### JsonEditor.razor (~300 lines)

Main editor with Form/JSON toggle:

**Model Detection Logic:**
- `*item*` → `RealmEngine.Shared.Models.Item`
- `*enemy*` → `RealmEngine.Shared.Models.Enemy`
- `*spell*` → `RealmEngine.Shared.Models.Spell`
- `*ability*` → `RealmEngine.Shared.Models.Ability`
- Unknown → JSON-only mode

**Mode Switching:**
- Form → JSON: Serialize model with `JsonSerializer.Serialize()`
- JSON → Form: Deserialize with `JsonSerializer.Deserialize<T>()`

**Features:**
- Two-panel layout (file tree + editor)
- Radio button mode toggle
- Bi-directional sync
- File operations (load/save/validate)

## Project Structure

```
RealmForge/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor      # App layout
│   │   └── NavMenu.razor         # Navigation menu
│   ├── Pages/
│   │   ├── Home.razor           # Welcome screen
│   │   └── JsonEditor.razor     # Main editor (~300 lines)
│   ├── Shared/
│   │   └── DynamicFormEditor.razor  # Generic form (~200 lines)
│   └── Routes.razor
├── Resources/
│   └── Images/
├── wwwroot/
│   ├── css/
│   └── index.html
├── MainPage.xaml               # MAUI entry point
├── MauiProgram.cs             # App configuration
└── RealmForge.csproj          # Project file
```

## Technology Stack

- **.NET 9.0** - Runtime
- **MAUI** - Cross-platform framework
- **Blazor** - Web UI framework
- **Razor Components** - UI components
- **WebView2** - Embedded browser (Windows)

## Why Blazor Hybrid?

**Advantages over WPF:**
✅ Simpler UI development (HTML/CSS/Razor vs XAML)  
✅ No complex data binding syntax  
✅ Web-standard layout (flexbox, grid)  
✅ Easier to maintain

**Advantages over Electron:**
✅ 100% C# codebase (no JavaScript/TypeScript)  
✅ Direct C# model integration  
✅ Smaller bundle size (~25-30MB vs 100+MB)  
✅ Better performance (native .NET runtime)

**Advantages over VS Code Extension:**
✅ Standalone deployable app  
✅ Ships with DLLs (no separate installation)  
✅ Doesn't require VS Code installed  
✅ Native desktop integration

**Shared Benefits:**
✅ Cross-platform ready (Windows, macOS, Linux)  
✅ Modern tooling (hot reload, dev tools)  
✅ Direct DLL integration (no IPC/CLI spawning)

## Known Limitations

1. **Simple Model Detection**: Based on filename string matching
   - TODO: Parse JSON metadata/type field for better detection

2. **Complex Types Not Editable**: Lists, Dictionaries, nested objects show as read-only
   - TODO: Recursive form generation or inline JSON editor for complex fields

3. **Basic JSON Editor**: Plain textarea
   - TODO: Integrate Monaco Editor for syntax highlighting, error detection

4. **No Validation Feedback**: Model validation not yet integrated
   - TODO: Display FluentValidation errors in form

## Technical Implementation

### Reflection-Based Property Inspection

```csharp
var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Where(p => p.CanRead && p.CanWrite)
    .Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
    .ToArray();
```

### Model Serialization

```csharp
// Form → JSON
var json = JsonSerializer.Serialize(model, new JsonSerializerOptions 
{ 
    WriteIndented = true 
});

// JSON → Form
var model = JsonSerializer.Deserialize<Item>(jsonContent);
```

### Enum Handling

```razor
<select class="form-select" 
        value="@GetEnumValue(prop)" 
        @onchange="@(e => SetEnumValue(prop, e))">
    @foreach (var enumVal in Enum.GetValues(prop.PropertyType))
    {
        <option value="@enumVal">@enumVal</option>
    }
</select>
```

## Building for Distribution

### Single-File Executable
```powershell
dotnet publish -c Release -f net9.0-windows10.0.19041.0 -p:PublishSingleFile=true
```

Output: Single `.exe` with all dependencies bundled (~25-30MB).
