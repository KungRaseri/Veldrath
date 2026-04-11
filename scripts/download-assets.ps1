<#
.SYNOPSIS
    Downloads the latest GameAssets release from KungRaseri/Veldrath-Assets and
    extracts it into Veldrath.Assets/GameAssets/.

.DESCRIPTION
    Fetches the latest GitHub release from the private KungRaseri/Veldrath-Assets
    repository, downloads the GameAssets zip asset, and extracts it so that local
    builds and the game client can use the assets without running sync-assets.ps1.

    A GitHub fine-grained PAT with "Contents: Read" on KungRaseri/Veldrath-Assets
    is required. Pass it via -Token or set the ASSETS_TOKEN environment variable.
    The CI secret ASSETS_TOKEN is already registered on the RealmEngine repo.

.PARAMETER Token
    GitHub PAT with Contents:Read on KungRaseri/Veldrath-Assets.
    Defaults to $env:ASSETS_TOKEN.

.PARAMETER Force
    Overwrite existing files in GameAssets/ without prompting.

.EXAMPLE
    .\scripts\download-assets.ps1
    .\scripts\download-assets.ps1 -Token ghp_xxxxx
    .\scripts\download-assets.ps1 -Force
#>

[CmdletBinding()]
param(
    [string]$Token = $env:ASSETS_TOKEN,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Validate token ────────────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Error ("No GitHub token supplied.`n" +
                 "Set the ASSETS_TOKEN environment variable or pass -Token <pat>.`n" +
                 "The PAT needs Contents:Read on KungRaseri/Veldrath-Assets.")
    exit 1
}

$dest = Join-Path $PSScriptRoot "..\Veldrath.Assets\GameAssets"
$dest = [System.IO.Path]::GetFullPath($dest)

$headers = @{
    "Authorization"        = "Bearer $Token"
    "Accept"               = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent"           = "RealmEngine-download-assets"
}

Write-Host ""
Write-Host "Veldrath.Assets -- downloading latest release" -ForegroundColor Cyan
Write-Host "  Repo : KungRaseri/Veldrath-Assets"
Write-Host "  Dest : $dest"
Write-Host ""

# ── Fetch latest release metadata ─────────────────────────────────────────────
Write-Host "Fetching latest release..." -NoNewline
try {
    $release = Invoke-RestMethod `
        -Uri "https://api.github.com/repos/KungRaseri/Veldrath-Assets/releases/latest" `
        -Headers $headers
} catch {
    Write-Host ""
    Write-Error "Failed to fetch release metadata: $_`nCheck that your token has Contents:Read on KungRaseri/Veldrath-Assets."
    exit 1
}
Write-Host " $($release.tag_name)"

# ── Locate the zip asset ──────────────────────────────────────────────────────
$asset = $release.assets | Where-Object { $_.name -like "GameAssets-v*.zip" } | Select-Object -First 1
if (-not $asset) {
    Write-Error "No GameAssets-v*.zip asset found in release $($release.tag_name).`nRun package-assets.ps1 and upload a new release first."
    exit 1
}

Write-Host "  Asset  : $($asset.name) ($([math]::Round($asset.size / 1MB, 1)) MB)"
Write-Host ""

# ── Guard against overwriting existing assets ─────────────────────────────────
if ((Test-Path $dest) -and (Get-ChildItem $dest -Recurse -File -ErrorAction SilentlyContinue).Count -gt 0) {
    if (-not $Force) {
        $answer = Read-Host "GameAssets/ already contains files. Overwrite? [y/N]"
        if ($answer -notmatch '^[Yy]') {
            Write-Host "Aborted." -ForegroundColor Yellow
            exit 0
        }
    }
}

# ── Download ──────────────────────────────────────────────────────────────────
$tmp = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.zip'
Write-Host "Downloading..." -NoNewline

try {
    # Step 1: ask the GitHub API for the asset download URL (302 redirect to S3).
    # Use MaximumRedirection 0 so we capture the Location header before following it.
    $redirectResponse = $null
    try {
        $redirectResponse = Invoke-WebRequest `
            -Uri "https://api.github.com/repos/KungRaseri/Veldrath-Assets/releases/assets/$($asset.id)" `
            -Headers ($headers + @{ "Accept" = "application/octet-stream" }) `
            -MaximumRedirection 0 `
            -SkipHttpErrorCheck
    } catch {
        # PowerShell throws on non-2xx when MaximumRedirection is 0; catch the 302
        $redirectResponse = $_.Exception.Response
    }

    $directUrl = $null
    if ($redirectResponse -is [Microsoft.PowerShell.Commands.WebResponseObject]) {
        $directUrl = $redirectResponse.Headers["Location"]
    } elseif ($redirectResponse -is [System.Net.HttpWebResponse]) {
        $directUrl = $redirectResponse.GetResponseHeader("Location")
        $redirectResponse.Dispose()
    }

    if ([string]::IsNullOrWhiteSpace($directUrl)) {
        # Fallback: the asset was small enough that GitHub served it directly (no redirect).
        # Re-download properly.
        Invoke-WebRequest `
            -Uri "https://api.github.com/repos/KungRaseri/Veldrath-Assets/releases/assets/$($asset.id)" `
            -Headers ($headers + @{ "Accept" = "application/octet-stream" }) `
            -OutFile $tmp
    } else {
        # Step 2: download from the presigned S3 URL (no auth header required).
        Invoke-WebRequest -Uri $directUrl -OutFile $tmp
    }
} catch {
    Write-Host ""
    Write-Error "Download failed: $_"
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    exit 1
}

$sizeMB = [math]::Round((Get-Item $tmp).Length / 1MB, 1)
Write-Host " $sizeMB MB"

# ── Extract ───────────────────────────────────────────────────────────────────
Write-Host "Extracting to $dest..."
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Expand-Archive -Path $tmp -DestinationPath $dest -Force
Remove-Item $tmp -Force

$fileCount = (Get-ChildItem $dest -Recurse -File).Count
Write-Host ""
Write-Host "Done -- $fileCount files in GameAssets/" -ForegroundColor Green
Write-Host ""
