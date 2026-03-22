<#
.SYNOPSIS
    Populates RealmUnbound.Assets/GameAssets/ from a local game-dev-assets source tree.

.DESCRIPTION
    Game asset binaries (PNG icons, OGG audio) are purchased packs that must not be
    committed to the public repository. This script performs a one-time (or incremental)
    copy from a developer-supplied source path into the project's GameAssets folder so
    that local builds and tests work correctly.

    Run this script once after cloning, or again whenever you add new asset packs to
    your local library.

.PARAMETER SourcePath
    Root of your local game-dev-assets library.
    Defaults to C:\code\_game-dev-assets (the standard developer machine path).

.EXAMPLE
    .\scripts\sync-assets.ps1
    .\scripts\sync-assets.ps1 -SourcePath D:\assets\_game-dev-assets
#>

[CmdletBinding()]
param(
    [string]$SourcePath = "C:\code\_game-dev-assets"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$dest = Join-Path $PSScriptRoot "..\RealmUnbound.Assets\GameAssets"
$dest = [System.IO.Path]::GetFullPath($dest)

if (-not (Test-Path $SourcePath)) {
    Write-Error "Source path not found: $SourcePath`nSet -SourcePath to your local game-dev-assets directory."
    exit 1
}

function Sync-Assets([string]$from, [string]$to, [string[]]$extensions = @("*.png","*.PNG","*.ogg")) {
    if (-not (Test-Path $from)) {
        Write-Warning "  Source missing, skipping: $from"
        return 0
    }
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    $count = 0
    foreach ($ext in $extensions) {
        $items = robocopy $from $to $ext /NJH /NJS /NP /NS /NC /XD addons 2>&1
        # robocopy exit codes 0-7 are success/partial; 8+ are errors
        if ($LASTEXITCODE -ge 8) { Write-Warning "  robocopy reported issues for $from (exit $LASTEXITCODE)" }
        $count += (Get-ChildItem $to -File -Filter $ext -ErrorAction SilentlyContinue).Count
    }
    return $count
}

Write-Host ""
Write-Host "RealmUnbound.Assets — game asset sync" -ForegroundColor Cyan
Write-Host "  Source : $SourcePath"
Write-Host "  Dest   : $dest"
Write-Host ""

# ── Enemies ──────────────────────────────────────────────────────────────────
Write-Host "Copying enemies..." -NoNewline
$n = Sync-Assets "$SourcePath\mobsavataricons_windows\mobsavataricons" "$dest\enemies"
Write-Host " $n files"

# ── Items ─────────────────────────────────────────────────────────────────────
Write-Host "Copying weapons..." -NoNewline
$n = Sync-Assets "$SourcePath\rpg_items\Weapons" "$dest\items\weapons"
Write-Host " $n files"

Write-Host "Copying armor..." -NoNewline
$n = Sync-Assets "$SourcePath\rpg_items\Armor" "$dest\items\armor"
Write-Host " $n files"

Write-Host "Copying potions..." -NoNewline
$n = Sync-Assets "$SourcePath\rpg_items\Potions" "$dest\items\potions"
Write-Host " $n files"

# ── Spells / Skills ────────────────────────────────────────────────────────────
Write-Host "Copying spells..." -NoNewline
$spellSrc = "$SourcePath\spellbookmegapack_windows\spellbookmegapack\Skill_Icon_Pack"
$total = 0
foreach ($color in @("blue","emerald","gray","green","red","violet","yellow")) {
    $total += Sync-Assets "$spellSrc\$color" "$dest\spells\$color"
}
Write-Host " $total files"

# ── Classes ────────────────────────────────────────────────────────────────────
Write-Host "Copying class badges..." -NoNewline
$n = Sync-Assets "$SourcePath\rpgclassbadges_windows\rpgclassbadges\Badge_png" "$dest\classes"
Write-Host " $n files"

# ── UI ─────────────────────────────────────────────────────────────────────────
Write-Host "Copying UI elements..." -NoNewline
$n = Sync-Assets "$SourcePath\woodenui\Wooden_UI_png" "$dest\ui"
Write-Host " $n files"

# ── Audio ──────────────────────────────────────────────────────────────────────
$kenneyAudio = "$SourcePath\Kenney Game Assets All-in-1 3.1.0\Audio"
Write-Host "Copying RPG audio..." -NoNewline
$n = Sync-Assets "$kenneyAudio\RPG Audio\Audio" "$dest\audio\rpg" @("*.ogg")
Write-Host " $n files"

Write-Host "Copying music loops..." -NoNewline
$n = Sync-Assets "$kenneyAudio\Music Loops\Loops" "$dest\audio\music" @("*.ogg")
Write-Host " $n files"

Write-Host "Copying impact sounds..." -NoNewline
$n = Sync-Assets "$kenneyAudio\Impact Sounds\Audio" "$dest\audio\impact" @("*.ogg")
Write-Host " $n files"

Write-Host "Copying interface sounds..." -NoNewline
$n = Sync-Assets "$kenneyAudio\Interface Sounds\Audio" "$dest\audio\interface" @("*.ogg")
Write-Host " $n files"

# ── Crafting ───────────────────────────────────────────────────────────────────
$ft = "$SourcePath\fairytaleiconsmegapack_windows\fairytaleiconsmegapack"
Write-Host "Copying crafting/mining..." -NoNewline
$n = Sync-Assets "$ft\MiningIcons\MiningIcons_transparent" "$dest\crafting\mining"
Write-Host " $n files"

Write-Host "Copying crafting/fishing..." -NoNewline
New-Item -ItemType Directory -Force -Path "$dest\crafting\fishing" | Out-Null
Get-ChildItem "$ft\FishingIcons\FishingIcons_png" -File |
    Where-Object { $_.Name -match "_t\.(png|PNG)$" -and $_.DirectoryName -notmatch "addons" } |
    Copy-Item -Destination "$dest\crafting\fishing\" -Force
$n = (Get-ChildItem "$dest\crafting\fishing" -File).Count
Write-Host " $n files"

Write-Host "Copying crafting/hunting..." -NoNewline
$n = Sync-Assets "$ft\HuntingIcons\HuntingIcons_transparent" "$dest\crafting\hunting"
Write-Host " $n files"

Write-Host "Copying crafting/forest..." -NoNewline
$n = Sync-Assets "$ft\ForestIcons\ForestIcons_transparent" "$dest\crafting\forest"
Write-Host " $n files"

# ── Extra item categories ──────────────────────────────────────────────────────
$ri = "$SourcePath\rpg_items"
Write-Host "Copying accessories..." -NoNewline
$n = Sync-Assets "$ri\Accessory" "$dest\items\accessories"
Write-Host " $n files"

Write-Host "Copying shields..." -NoNewline
$n = Sync-Assets "$ri\Shields" "$dest\items\shields"
Write-Host " $n files"

Write-Host "Copying food..." -NoNewline
$n = Sync-Assets "$ri\Food" "$dest\items\food"
Write-Host " $n files"

Write-Host "Copying misc items..." -NoNewline
$n = Sync-Assets "$ri\Misc" "$dest\items\misc"
Write-Host " $n files"

Write-Host "Copying crafting ingredients..." -NoNewline
$n = Sync-Assets "$ri\Crafting&Gathering" "$dest\items\crafting-ingredients"
Write-Host " $n files"

# ── Summary ────────────────────────────────────────────────────────────────────
$totalFiles = (Get-ChildItem $dest -File -Recurse).Count
$totalSize  = [math]::Round((Get-ChildItem $dest -File -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ""
Write-Host "Done. $totalFiles files ($totalSize MB) in $dest" -ForegroundColor Green
Write-Host ""
