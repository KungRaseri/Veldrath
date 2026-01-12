# RealmForge - Blazor Hybrid JSON Editor

**Status**: ✅ Functional - Basic JSON editing capability  
**Technology**: .NET MAUI Blazor Hybrid  
**Target**: Windows Desktop (cross-platform ready)

## Overview

RealmForge is a desktop application for editing RealmEngine's JSON data files. Built with Blazor Hybrid, it provides a simple web-based UI in a native desktop window.

## Features

### ✅ Implemented
- Browse JSON files in Data/Json folder
- Edit JSON content in textarea
- Save changes to files
- Basic JSON validation

### 🚧 Planned
- Syntax highlighting (Monaco Editor)
- Schema validation (using RealmEngine validators)
- TreeView for nested JSON
- Search and filter files
- Undo/Redo
- File system picker

## Running the App

### Development
```powershell
cd RealmForge
dotnet run -f net9.0-windows10.0.19041.0
```

### Build Release
```powershell
dotnet publish -f net9.0-windows10.0.19041.0 -c Release
```

## Project Structure

```
RealmForge/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Home.razor
│   │   └── JsonEditor.razor
│   └── Routes.razor
├── Resources/
│   └── Images/
├── wwwroot/
│   ├── css/
│   └── index.html
├── MainPage.xaml
├── MauiProgram.cs
└── RealmForge.csproj
```

## Technology Stack

- **.NET 9.0** - Runtime
- **MAUI** - Cross-platform framework
- **Blazor** - Web UI framework
- **Razor Components** - UI components
- **WebView2** - Embedded browser (Windows)

## Why Blazor Hybrid?

✅ **Simpler than WPF** - Write HTML/CSS instead of XAML  
✅ **100% C#** - No JavaScript required  
✅ **Direct DLL integration** - Reference RealmEngine projects  
✅ **Cross-platform** - Can target Mac/Linux if needed  
✅ **Modern tooling** - Hot reload, dev tools

## Integration with RealmEngine

RealmForge references:
- `RealmEngine.Shared.dll` - Data models
- `RealmEngine.Data.dll` - JSON loaders and validators

This allows direct use of existing validation logic without spawning CLI processes.

## Next Steps

1. Add Monaco Editor for syntax highlighting
2. Wire up JSON schema validation
3. Add file tree view with expand/collapse
4. Implement proper folder picker
5. Add search functionality

## Building for Distribution

Once ready, package with:
```powershell
dotnet publish -c Release -f net9.0-windows10.0.19041.0 -p:PublishSingleFile=true
```

Output: Single `.exe` with all dependencies bundled.
