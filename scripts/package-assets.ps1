<#
.SYNOPSIS
    Packages RealmUnbound.Assets/GameAssets/ into a versioned zip for upload as a
    GitHub Release asset in the private KungRaseri/RealmEngine-Assets repository.

.DESCRIPTION
    Game asset binaries live outside git (gitignored). This script compresses the
    local GameAssets folder into a single zip file ready for upload to a private
    GitHub Release. CI then downloads this release asset instead of requiring
    developers to run sync-assets.ps1 on every machine.

    Workflow:
      1. Run sync-assets.ps1 to populate GameAssets/ from your local pack library.
      2. Run this script to create GameAssets-v<version>.zip.
      3. Upload to the private GitHub release (command printed by this script).
      4. Ensure ASSETS_TOKEN secret is set in RealmEngine repo settings (see below).

    Required GitHub setup (one-time):
    Create a private repo: KungRaseri/RealmUnbound-Assets
      - Create a fine-grained PAT with "Contents: Read and Write" on that repo
        for packaging/uploading, and "Contents: Read" for CI downloads.
      - Add the read-only PAT as secret ASSETS_TOKEN in KungRaseri/RealmEngine
        (repo Settings --> Secrets and variables --> Actions --> New repository secret).

.PARAMETER Version
    Version label for the zip file and GitHub Release tag.
    Defaults to "1.0".

.PARAMETER OutputDir
    Directory where the zip will be written. Defaults to the scripts/ folder.

.EXAMPLE
    .\scripts\package-assets.ps1
    .\scripts\package-assets.ps1 -Version 1.1 -OutputDir C:\Temp
#>

[CmdletBinding()]
param(
    [string]$Version = "1.0",
    [string]$OutputDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$source = Join-Path $PSScriptRoot "..\RealmUnbound.Assets\GameAssets"
$source = [System.IO.Path]::GetFullPath($source)

if (-not (Test-Path $source)) {
    Write-Error "GameAssets not found at: $source`nRun sync-assets.ps1 first to populate it."
    exit 1
}

$fileCount = (Get-ChildItem $source -Recurse -File -ErrorAction SilentlyContinue).Count
if ($fileCount -eq 0) {
    Write-Error "GameAssets/ is empty at: $source`nRun sync-assets.ps1 first to populate it."
    exit 1
}

$zipName = "GameAssets-v$Version.zip"
$zipPath = Join-Path $OutputDir $zipName

Write-Host ""
Write-Host "RealmUnbound.Assets -- packaging game assets" -ForegroundColor Cyan
Write-Host "  Source  : $source ($fileCount files)"
Write-Host "  Output  : $zipPath"
Write-Host "  Version : v$Version"
Write-Host ""
Write-Host "Compressing..." -NoNewline

# Compress in a way that preserves the relative folder structure
# (zip contains enemies/, items/, spells/... directly at root so Expand-Archive
#  into GameAssets/ produces GameAssets/enemies/ etc.)
Compress-Archive -Path "$source\*" -DestinationPath $zipPath -Force

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host " $sizeMB MB"
Write-Host ""
Write-Host "Done: $zipPath" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Ensure you have 'Contents: write' access to KungRaseri/RealmEngine-Assets"
Write-Host "  2. Run the command below to create (or update) the GitHub Release:"
Write-Host ""
Write-Host "     gh release create v$Version ``" -ForegroundColor White
 Write-Host "       --repo KungRaseri/RealmUnbound-Assets ``" -ForegroundColor White
Write-Host "       --title `"GameAssets v$Version`" ``" -ForegroundColor White
Write-Host "       --notes `"Game asset pack v$Version - see RealmUnbound.Assets for manifest`" ``" -ForegroundColor White
Write-Host "       `"$zipPath`"" -ForegroundColor White
Write-Host ""
Write-Host "  3. If updating an existing release, use 'gh release upload' instead:"
Write-Host ""
Write-Host "     gh release upload v$Version --repo KungRaseri/RealmUnbound-Assets ``" -ForegroundColor White
Write-Host "       --clobber `"$zipPath`"" -ForegroundColor White
Write-Host ""
Write-Host "  4. Add secret ASSETS_TOKEN in KungRaseri/RealmEngine if not already set."
Write-Host "     (repo Settings --> Secrets and variables --> Actions --> New repository secret)"
Write-Host ""
