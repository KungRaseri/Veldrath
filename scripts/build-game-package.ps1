# Build and Package Script for Godot Integration
param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "package"
)

$ErrorActionPreference = "Stop"

Write-Output "======================================"
Write-Output "  Game Package Build Script"
Write-Output "======================================"
Write-Output ""

# Get solution root
$SolutionRoot = Split-Path $PSScriptRoot -Parent
$PackageRoot = Join-Path $SolutionRoot $OutputPath

# Generate version
Write-Output "Generating version..."
$VersionScript = Join-Path $PSScriptRoot "generate-version.ps1"
$Version = & $VersionScript -OutputFormat "string"
$VersionArgs = @(& $VersionScript -OutputFormat "msbuild")

Write-Output "Building version: $Version"
Write-Output ""

# Clean output directory
Write-Output "Cleaning output directory..."
if (Test-Path $PackageRoot) {
    Remove-Item $PackageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $PackageRoot | Out-Null

# Create package structure
$Directories = @(
    "Libraries",
    "RealmForge",
    "Data\Json"
)

foreach ($dir in $Directories) {
    New-Item -ItemType Directory -Path (Join-Path $PackageRoot $dir) -Force | Out-Null
}

Write-Output "Package output: $PackageRoot"
Write-Output ""

# Build RealmEngine.Core
Write-Output "Building RealmEngine.Core..."
$CoreOutput = Join-Path $PackageRoot "Libraries\RealmEngine.Core"
dotnet publish (Join-Path $SolutionRoot "RealmEngine.Core\RealmEngine.Core.csproj") --configuration $Configuration --output $CoreOutput --no-self-contained --verbosity quiet $VersionArgs
if ($LASTEXITCODE -ne 0) { Write-Error "RealmEngine.Core build failed!"; exit 1 }
Write-Output "[OK] RealmEngine.Core published (with XML docs)"
Write-Output ""

# Build RealmEngine.Shared
Write-Output "Building RealmEngine.Shared..."
$SharedOutput = Join-Path $PackageRoot "Libraries\RealmEngine.Shared"
dotnet publish (Join-Path $SolutionRoot "RealmEngine.Shared\RealmEngine.Shared.csproj") --configuration $Configuration --output $SharedOutput --no-self-contained --verbosity quiet $VersionArgs
if ($LASTEXITCODE -ne 0) { Write-Error "RealmEngine.Shared build failed!"; exit 1 }
Write-Output "[OK] RealmEngine.Shared published (with XML docs)"
Write-Output ""

# Build RealmEngine.Data
Write-Output "Building RealmEngine.Data..."
$DataOutput = Join-Path $PackageRoot "Libraries\RealmEngine.Data"
dotnet publish (Join-Path $SolutionRoot "RealmEngine.Data\RealmEngine.Data.csproj") --configuration $Configuration --output $DataOutput --no-self-contained --verbosity quiet $VersionArgs
if ($LASTEXITCODE -ne 0) { Write-Error "RealmEngine.Data build failed!"; exit 1 }
Write-Output "[OK] RealmEngine.Data published (with XML docs)"
Write-Output ""

# Build RealmForge
Write-Output "Building RealmForge..."
$ContentBuilderOutput = Join-Path $PackageRoot "RealmForge"
dotnet publish (Join-Path $SolutionRoot "RealmForge\RealmForge.csproj") --configuration $Configuration --framework net9.0-windows10.0.19041.0 --output $ContentBuilderOutput --no-self-contained --verbosity quiet $VersionArgs
if ($LASTEXITCODE -ne 0) { Write-Error "RealmForge build failed!"; exit 1 }

# Remove duplicate Data folder from RealmForge (it will reference package root Data)
$ContentBuilderDataPath = Join-Path $ContentBuilderOutput "Data"
if (Test-Path $ContentBuilderDataPath) {
    Remove-Item $ContentBuilderDataPath -Recurse -Force
    Write-Output "[CLEANUP] Removed duplicate Data folder from RealmForge"
}

# Create RealmForge config pointing to package Data location
$ContentBuilderConfig = @{
    DataPath = "..\Data\Json"
    Description = "RealmForge will use Data\Json at package root"
} | ConvertTo-Json
Set-Content -Path (Join-Path $ContentBuilderOutput "RealmForge.config.json") -Value $ContentBuilderConfig

Write-Output "[OK] RealmForge published"
Write-Output ""

# Copy JSON Data Files
Write-Output "Copying JSON data files..."
$JsonSource = Join-Path $SolutionRoot "RealmEngine.Data\Data\Json"
$JsonDest = Join-Path $PackageRoot "Data\Json"
Copy-Item -Path "$JsonSource\*" -Destination $JsonDest -Recurse -Force
$JsonFileCount = (Get-ChildItem -Path $JsonDest -Recurse -File).Count
Write-Output "[OK] Copied $JsonFileCount JSON files"
Write-Output ""

# Generate Package Manifest
Write-Output "Generating package manifest..."
$Manifest = @{
    Version = $Version
    PackageDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Configuration = $Configuration
    Components = @{
        GameCore = @{
            Path = "Libraries\RealmEngine.Core"
            Assembly = "RealmEngine.Core.dll"
        }
        GameShared = @{
            Path = "Libraries\RealmEngine.Shared"
            Assembly = "RealmEngine.Shared.dll"
        }
        GameData = @{
            Path = "Libraries\RealmEngine.Data"
            Assembly = "RealmEngine.Data.dll"
        }
        RealmForge = @{
            Path = "RealmForge"
            Executable = "RealmForge.exe"
        }
        JsonData = @{
            Path = "Data\Json"
            FileCount = $JsonFileCount
        }
    }
}

$ManifestJson = $Manifest | ConvertTo-Json -Depth 10
$ManifestPath = Join-Path $PackageRoot "package-manifest.json"
Set-Content -Path $ManifestPath -Value $ManifestJson
Write-Output "[OK] Package manifest generated"
Write-Output ""

# Generate Release Notes with Git Diff
Write-Output "Generating release notes..."
$ReleaseNotesPath = Join-Path $PackageRoot "release-notes.md"

# Get current commit hash
$CurrentCommit = git -C $SolutionRoot rev-parse HEAD 2>$null
$CurrentCommitShort = git -C $SolutionRoot rev-parse --short HEAD 2>$null
$CurrentBranch = git -C $SolutionRoot rev-parse --abbrev-ref HEAD 2>$null

if (-not $CurrentCommit) {
    Write-Output "[WARNING] Not a git repository, skipping release notes generation"
} else {
    # Get latest git tag
    $LatestTag = git -C $SolutionRoot describe --tags --abbrev=0 2>$null
    $TagCommit = if ($LatestTag) { git -C $SolutionRoot rev-parse "$LatestTag^{}" 2>$null } else { $null }

    # Generate release notes
    $ReleaseNotes = @"
# Release Notes - $Version
**Build Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Configuration:** $Configuration

## Git Commit Information
- **Current Commit:** ``$CurrentCommit``
- **Short Hash:** ``$CurrentCommitShort``
- **Branch:** ``$CurrentBranch``
$(if ($LatestTag) { "- **Latest Tag:** ``$LatestTag`` (commit: ``$TagCommit``)" } else { "- **Latest Tag:** *(No tags found in repository)*" })

"@

    # Generate git diff if tag exists
    if ($LatestTag -and $TagCommit -ne $CurrentCommit) {
        Write-Output "Generating diff from tag $LatestTag to $CurrentCommitShort..."
        
        # Get commit log since tag
        $CommitLog = git -C $SolutionRoot log --oneline "$LatestTag..HEAD" 2>$null
        $CommitCount = ($CommitLog | Measure-Object -Line).Lines
        
        # Get changed files in API assemblies
        $ApiPaths = @(
            "RealmEngine.Core/**/*.cs"
            "RealmEngine.Shared/**/*.cs"
            "RealmEngine.Data/**/*.cs"
        )
        
        $ChangedApiFiles = git -C $SolutionRoot diff --name-only "$LatestTag..HEAD" -- $ApiPaths 2>$null
        
        # Get detailed diff for API files
        $ApiDiff = git -C $SolutionRoot diff "$LatestTag..HEAD" -- $ApiPaths 2>$null
        
        # Get stats about the changes
        $DiffStats = git -C $SolutionRoot diff --stat "$LatestTag..HEAD" -- $ApiPaths 2>$null
        
        # Format commit log - one per line
        $FormattedCommitLog = $CommitLog -split "`n" | ForEach-Object { "- $_" }
        
        # Get file status (added/modified/deleted) for each changed API file
        $FileStatusList = @()
        foreach ($file in $ChangedApiFiles) {
            $status = git -C $SolutionRoot diff --name-status "$LatestTag..HEAD" -- $file 2>$null
            if ($status -match '^([AMD])') {
                $statusChar = switch ($matches[1]) {
                    'A' { '+' }
                    'M' { '±' }
                    'D' { '-' }
                    default { ' ' }
                }
                $FileStatusList += "$statusChar $file"
            }
        }
        
        $ReleaseNotes += @"

## Changes Since Tag ``$LatestTag``
**Commits:** $CommitCount

### Commit History
$($FormattedCommitLog -join "`n")

## API Changes Summary
**Changed Files:** $($ChangedApiFiles.Count)

$(if ($DiffStats) {
    "### Change Statistics"
    "``````text"
    $DiffStats
    "``````"
    ""
})

$(if ($ChangedApiFiles) {
    "### Modified API Files"
    "``````diff"
    $FileStatusList -join "`n"
    "``````"
    "**Legend:** ``+`` = Added, ``±`` = Modified, ``-`` = Deleted"
    ""
} else {
    "*(No API changes detected)*"
    ""
})

## Detailed API Diff
<details>
<summary>Click to expand full diff ($($ChangedApiFiles.Count) files changed)</summary>

``````diff
$ApiDiff
``````

**Note:** Lines starting with `+` are additions, `-` are deletions.

</details>

"@
    } elseif ($TagCommit -eq $CurrentCommit) {
        $ReleaseNotes += @"

## Changes
*(No changes - current commit matches tag ``$LatestTag``)*

"@
    } else {
        $ReleaseNotes += @"

## Changes
*(No tags found - showing full codebase in this package)*

"@
    }

    # Add package contents
    $ReleaseNotes += @"

## Package Contents
- **RealmEngine.Core** - Core game mechanics (Commands, Handlers, Services)
- **RealmEngine.Shared** - Shared models and abstractions
- **RealmEngine.Data** - Data loading and caching services
- **RealmForge** - JSON content editor (WPF application)
- **Data\Json** - $JsonFileCount game data files

## Integration Instructions
1. Copy ``Libraries\`` folder to Godot project's ``csharp\libraries\`` directory
2. Copy ``Data\Json\`` folder to Godot project's ``data\`` directory
3. Add assembly references in Godot's ``.csproj`` file:
   - ``RealmEngine.Core.dll``
   - ``RealmEngine.Shared.dll``
   - ``RealmEngine.Data.dll``

## API Usage
See XML documentation files (``.xml``) in each Libraries folder for complete API reference.

---
*Generated by build-game-package.ps1*
*Compare against tag: $LatestTag*
"@

    Set-Content -Path $ReleaseNotesPath -Value $ReleaseNotes
    
    Write-Output "[OK] Release notes generated: release-notes.md"
    if ($LatestTag) {
        Write-Output "     Baseline: $LatestTag ($($TagCommit.Substring(0, 7)))"
        Write-Output "     Current:  $CurrentCommitShort"
        if ($CommitCount -gt 0) {
            Write-Output "     Commits:  $CommitCount since tag"
        }
        if ($ChangedApiFiles) {
            Write-Output "     API Changes: $($ChangedApiFiles.Count) files modified"
        }
    } else {
        Write-Output "     Current:  $CurrentCommitShort (no tags for comparison)"
    }
}
Write-Output ""

# Summary
Write-Output "======================================"
Write-Output "  Build Complete!"
Write-Output "======================================"
Write-Output ""
Write-Output "Package Location: $PackageRoot"
Write-Output ""
Write-Output "Package Contents:"
Write-Output "  - Libraries\RealmEngine.Core\      (Core game mechanics)"
Write-Output "  - Libraries\RealmEngine.Shared\    (Shared models/services)"
Write-Output "  - Libraries\RealmEngine.Data\      (Data loading)"
Write-Output "  - RealmForge\           (JSON editor application)"
Write-Output "  - Data\Json\                (Game data: $JsonFileCount files)"
Write-Output ""
Write-Output "Next: Run deploy-to-godot.ps1 to copy to Godot project"
Write-Output ""
