# RealmForge - Game Data Editor & Modding Tool

**Version**: 3.0 (Blazor Hybrid)  
**Last Updated**: January 12, 2026  
**Status**: Active Development  
**Technology**: .NET MAUI Blazor Hybrid

---

## Overview

RealmForge is a cross-platform desktop application for editing RealmEngine JSON game data and creating game mods. Built with .NET MAUI Blazor Hybrid, it provides a form-based interface backed by RealmEngine.Shared models with JSON fallback for advanced users.

### Key Features

- **Dynamic Form Editor** - Auto-generated forms from C# models
- **Form/JSON Toggle** - Structured editing with raw JSON fallback
- **Model-Driven** - Direct integration with RealmEngine.Shared models
- **Modding Support** - Create, package, and distribute mods (planned)

---

## Current Status (v3.0)

### ✅ Implemented
- Dynamic form generation from models using reflection
- Form/JSON mode toggle with bi-directional sync
- File browser for JSON data navigation
- Model detection (Item, Enemy, Spell, Ability)
- Type-specific form inputs (string, int, double, bool, enum)
- Basic save/load functionality

### 🔄 In Progress
- Monaco Editor integration for JSON mode
- FluentValidation error display
- Complex property editing (lists, nested objects)
- Native folder picker

### 📋 Planned
- Multi-file tabbed editing
- Reference browser with visual picker
- Undo/redo system
- Mod project management
- Mod packaging and distribution

See [Roadmap](features/roadmap.md) for detailed timeline.

---

## Architecture

**Technology Stack:**
- .NET 9.0 MAUI Blazor Hybrid
- Blazor Components (HTML/CSS/Razor)
- RealmEngine.Shared (models)
- RealmEngine.Data (data access)
- System.Reflection (dynamic forms)

**Key Components:**
- `DynamicFormEditor.razor` - Generic form generator
- `JsonEditor.razor` - Form/JSON toggle editor
- `Home.razor` - Welcome screen

See [Architecture](features/architecture.md) for details.

---

## Running RealmForge

```powershell
# Development
dotnet run --project RealmForge/RealmForge.csproj -f net9.0-windows10.0.19041.0

# Build
dotnet build RealmForge/RealmForge.csproj -f net9.0-windows10.0.19041.0
```

---

## Documentation

### Features
- [JSON Editing](features/json-editing.md) - Form/JSON editor features
- [Form System](features/form-system.md) - Dynamic form generation
- [Modding Framework](features/modding-framework.md) - Mod creation and management
- [Validation System](features/validation-system.md) - Data validation
- [Roadmap](features/roadmap.md) - Development timeline

### Technical
- [Architecture](features/architecture.md) - Technical design
- [Models](features/models.md) - RealmEngine.Shared model integration
- [File Formats](features/file-formats.md) - JSON v5.1 standards

---

## Requirements

- Windows 10/11 (64-bit) - *Current*
- macOS 10.15+ - *Planned*
- Linux (Ubuntu 20.04+) - *Planned*
- .NET 9.0 Runtime
- 150 MB disk space
- 2 GB RAM minimum

---

## License

Part of the RealmEngine project. See main repository LICENSE.

---

## Version History

- **v3.0** (Jan 12, 2026) - Blazor Hybrid rewrite, dynamic forms
- **v2.0** (Jan 5, 2026) - WPF implementation, JSON v4.1 support
- **v1.0** (Dec 2025) - Initial release
