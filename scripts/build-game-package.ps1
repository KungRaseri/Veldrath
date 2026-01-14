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
dotnet publish (Join-Path $SolutionRoot "RealmForge\RealmForge.csproj") --configuration $Configuration --output $ContentBuilderOutput --no-self-contained --runtime win-x64 --verbosity quiet $VersionArgs
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
