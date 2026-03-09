# Create and push a release tag for a specific component.
#
# Usage:
#   .\scripts\create-release-tag.ps1 -Component engine
#   .\scripts\create-release-tag.ps1 -Component tooling
#   .\scripts\create-release-tag.ps1 -Component server
#   .\scripts\create-release-tag.ps1 -Component client
#   .\scripts\create-release-tag.ps1 -Component engine -DryRun
#
# This is for manually triggering a release without waiting for CI to auto-tag.
# Reads the version from versions/<component>.props + current commit count.

param(
    [Parameter(Mandatory)]
    [ValidateSet('engine', 'tooling', 'server', 'client')]
    [string]$Component,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$PropsFile = Join-Path $PSScriptRoot "..\versions\$Component.props"
$version = & "$PSScriptRoot\generate-version.ps1" -OutputFormat "string" -PropsFile $PropsFile
$versionNoHash = $version -replace '-[^-]+$', ''
$tag = "$Component/v$versionNoHash"

Write-Output "Component : $Component"
Write-Output "Version   : $versionNoHash"
Write-Output "Tag       : $tag"

# Check tag does not already exist
$existing = git tag --list $tag
if ($existing) {
    Write-Error "Tag '$tag' already exists. Bump VersionMajor/VersionMinor in versions/$Component.props first."
    exit 1
}

if ($DryRun) {
    Write-Output ""
    Write-Output "[DRY RUN] Would create and push tag: $tag"
    exit 0
}

git tag $tag
Write-Output "Created local tag: $tag"

git push origin $tag
Write-Output "Pushed tag: $tag"
Write-Output ""
Write-Output "Release workflow will trigger at:"
Write-Output "  https://github.com/$(git remote get-url origin | Select-String -Pattern 'github.com[:/](.+?)(.git)?$' | ForEach-Object { $_.Matches[0].Groups[1].Value })/actions"
