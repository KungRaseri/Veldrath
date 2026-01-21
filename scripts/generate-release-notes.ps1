#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate GitHub release notes for RealmEngine
.DESCRIPTION
    Creates a formatted release notes markdown file with package information, changelog, and documentation links
.PARAMETER Version
    The version number for this release (e.g., "1.0.0")
.PARAMETER JsonCount
    The number of JSON data files in the package
.PARAMETER Changelog
    The changelog content (commit messages)
.PARAMETER Commit
    The commit SHA for this release
.PARAMETER RepoUrl
    The GitHub repository URL
.PARAMETER RepoOwner
    The GitHub repository owner
.PARAMETER OutputFile
    The output file path for release notes (default: release-notes.md)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$JsonCount,
    
    [Parameter(Mandatory=$true)]
    [string]$Changelog,
    
    [Parameter(Mandatory=$true)]
    [string]$Commit,
    
    [Parameter(Mandatory=$true)]
    [string]$RepoUrl,
    
    [Parameter(Mandatory=$true)]
    [string]$RepoOwner,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "release-notes.md"
)

$sb = [System.Text.StringBuilder]::new()

# Header
[void]$sb.AppendLine("## RealmEngine v$version")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> **Game Engine Backend for Godot Integration**")
[void]$sb.AppendLine("> Built with .NET 9.0 | C# | MediatR CQRS | LiteDB")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("A complete game engine backend providing:")
[void]$sb.AppendLine("- 🎮 **Game Logic** - Combat, inventory, progression, quests, NPCs")
[void]$sb.AppendLine("- 📊 **Data Models** - Character, Item, Enemy, Ability, Location")
[void]$sb.AppendLine("- 🔧 **Generators** - Procedural items, enemies, NPCs, loot")
[void]$sb.AppendLine("- 💾 **Persistence** - Save/load system with LiteDB")
[void]$sb.AppendLine("- 🎯 **MediatR Commands** - CreateCharacter, AttackEnemy, BuyFromShop")
[void]$sb.AppendLine("- 🔍 **MediatR Queries** - GetPlayerInventory, GetCombatState, GetActiveQuests")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### What's Included")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- **Game Libraries** - Core engine DLLs with XML IntelliSense documentation")
[void]$sb.AppendLine("- **RealmForge** - MAUI desktop/mobile editor for JSON game data (v4.0+ standards)")
[void]$sb.AppendLine("- **Game Data** - $jsonCount JSON files (abilities, items, enemies, NPCs, quests, etc.)")
[void]$sb.AppendLine("- **NuGet Packages** - Published to GitHub Packages")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Downloads")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Package | Description |")
[void]$sb.AppendLine("|---------|-------------|")
[void]$sb.AppendLine("| **RealmEngine-Full-$version.zip** | 📦 Complete package (libraries + data) |")
[void]$sb.AppendLine("| **RealmEngine-Libraries-$version.zip** | 📚 DLLs only (for Godot C# integration) |")
[void]$sb.AppendLine("| **RealmEngine-Data-$version.zip** | 📂 JSON data files only |")
[void]$sb.AppendLine("| **RealmForge-Windows-$version.zip** | 🛠️ JSON Editor for Windows + Data |")
[void]$sb.AppendLine("| **RealmForge-Android-$version.zip** | 📱 JSON Editor for Android + Data |")
[void]$sb.AppendLine("| **package-manifest.json** | 📋 Build metadata and file inventory |")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Installation")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("#### For Godot Integration")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("1. Download ``RealmEngine-Libraries-$version.zip``")
[void]$sb.AppendLine("2. Extract to your Godot project folder")
[void]$sb.AppendLine("3. Reference DLLs in your C# scripts:")
[void]$sb.AppendLine("   ````csharp")
[void]$sb.AppendLine("   using RealmEngine.Core;")
[void]$sb.AppendLine("   using RealmEngine.Shared.Models;")
[void]$sb.AppendLine("   ````")
[void]$sb.AppendLine("4. IntelliSense documentation is automatically available")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("#### For Content Editing (Windows)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("1. Download ``RealmForge-Windows-$version.zip``")
[void]$sb.AppendLine("2. Extract and install the MSIX package")
[void]$sb.AppendLine("3. Launch RealmForge from Start Menu")
[void]$sb.AppendLine("4. Edit JSON game data with visual validation")
[void]$sb.AppendLine("5. All changes comply with JSON v4.0+ standards")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("#### For Content Editing (Android)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("1. Download ``RealmForge-Android-$version.zip``")
[void]$sb.AppendLine("2. Extract and sideload the APK")
[void]$sb.AppendLine("3. Edit game data on mobile devices")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("#### Via NuGet (GitHub Packages)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("````bash")
[void]$sb.AppendLine("dotnet add package RealmEngine.Core --version $version --source https://nuget.pkg.github.com/$repoOwner/index.json")
[void]$sb.AppendLine("dotnet add package RealmEngine.Shared --version $version --source https://nuget.pkg.github.com/$repoOwner/index.json")
[void]$sb.AppendLine("dotnet add package RealmEngine.Data --version $version --source https://nuget.pkg.github.com/$repoOwner/index.json")
[void]$sb.AppendLine("````")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Requirements")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- **.NET 9.0 Runtime** (bundled with RealmForge packages)")
[void]$sb.AppendLine("- **Godot 4.x with C# support** (for game integration)")
[void]$sb.AppendLine("- **Windows 10+ (10.0.19041.0)** (for RealmForge Windows - MAUI application)")
[void]$sb.AppendLine("- **Android 7.0+ (API 24)** (for RealmForge Android)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Package Contents")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("**Libraries** (RealmEngine-Libraries-*.zip):")
[void]$sb.AppendLine("- ``RealmEngine.Core.dll`` + ``.xml`` - Game mechanics (generators, validators, combat)")
[void]$sb.AppendLine("- ``RealmEngine.Shared.dll`` + ``.xml`` - Shared models and services")
[void]$sb.AppendLine("- ``RealmEngine.Data.dll`` + ``.xml`` - JSON data loading and validation")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("**RealmForge** (RealmForge-Windows/Android-*.zip):")
[void]$sb.AppendLine("- MAUI Blazor Hybrid application")
[void]$sb.AppendLine("- Visual JSON editor with Monaco editor")
[void]$sb.AppendLine("- Real-time validation and reference resolution")
[void]$sb.AppendLine("- All dependencies and runtime files included")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("**Data** (RealmEngine-Data-*.zip or included in Full package):")
[void]$sb.AppendLine("- $jsonCount JSON files organized by domain")
[void]$sb.AppendLine("- v4.0+ compliant with reference system (v4.1) support")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Changes in This Release")
[void]$sb.AppendLine("")

# Add changelog content
[void]$sb.AppendLine($Changelog)

[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Documentation")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- [Game Design Document]($repoUrl/blob/main/docs/GDD-Main.md)")
[void]$sb.AppendLine("- [JSON v4.0 Standards]($repoUrl/blob/main/docs/standards/json/README.md)")
[void]$sb.AppendLine("- [Reference System Guide]($repoUrl/blob/main/docs/standards/json/JSON_REFERENCE_STANDARDS.md)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Support")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Found an issue? [Report it here]($repoUrl/issues)")

# Write to file
$sb.ToString() | Out-File -FilePath $OutputFile -Encoding utf8
Write-Output "Release notes generated: $OutputFile"
