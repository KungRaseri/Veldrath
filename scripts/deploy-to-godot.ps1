# Deploy to Godot Project Script
# Copies packaged game files to Godot project directory

param(
    [Parameter(Mandatory=$true)]
    [string]$GodotProjectPath,
    
    [string]$PackagePath = "package"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Deploy to Godot Project" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Validate paths
$SolutionRoot = Split-Path $PSScriptRoot -Parent
$PackageRoot = Join-Path $SolutionRoot $PackagePath

if (-not (Test-Path $PackageRoot)) {
    Write-Error "Package not found at: $PackageRoot"
    Write-Host "Run build-game-package.ps1 first!" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $GodotProjectPath)) {
    Write-Error "Godot project not found at: $GodotProjectPath"
    exit 1
}

# Check for project.godot
$GodotProjectFile = Join-Path $GodotProjectPath "project.godot"
if (-not (Test-Path $GodotProjectFile)) {
    Write-Error "Not a valid Godot project (project.godot not found)"
    exit 1
}

Write-Host "Source Package: $PackageRoot" -ForegroundColor Gray
Write-Host "Target Godot:   $GodotProjectPath" -ForegroundColor Gray
Write-Host ""

# Read package manifest
$ManifestPath = Join-Path $PackageRoot "package-manifest.json"
if (Test-Path $ManifestPath) {
    $Manifest = Get-Content $ManifestPath | ConvertFrom-Json
    Write-Host "Package Date: $($Manifest.PackageDate)" -ForegroundColor Gray
    Write-Host "Configuration: $($Manifest.Configuration)" -ForegroundColor Gray
    Write-Host ""
}

# ============================================
# 1. Generate Changelog from XML Differences
# ============================================
Write-Host "Analyzing XML documentation changes..." -ForegroundColor Yellow

$LibrariesSource = Join-Path $PackageRoot "Libraries"
$LibrariesDest = Join-Path $GodotProjectPath "Libraries"

$ChangelogEntries = @()
$HasChanges = $false

# Get all XML files from source
$NewXmlFiles = Get-ChildItem -Path $LibrariesSource -Filter "*.xml"

foreach ($newXml in $NewXmlFiles) {
    $oldXmlPath = Join-Path $LibrariesDest $newXml.Name
    
    if (Test-Path $oldXmlPath) {
        # Compare existing file
        $oldContent = Get-Content $oldXmlPath -Raw
        $newContent = Get-Content $newXml.FullName -Raw
        
        if ($oldContent -ne $newContent) {
            Write-Host "  • Changes detected in $($newXml.Name)" -ForegroundColor Cyan
            
            # Parse XML to extract changes
            try {
                [xml]$oldXmlDoc = $oldContent
                [xml]$newXmlDoc = $newContent
                
                # Compare member counts
                $oldMembers = $oldXmlDoc.SelectNodes("//member")
                $newMembers = $newXmlDoc.SelectNodes("//member")
                
                $oldMemberNames = @($oldMembers | ForEach-Object { $_.name })
                $newMemberNames = @($newMembers | ForEach-Object { $_.name })
                
                # Find added members
                $addedMembers = $newMemberNames | Where-Object { $_ -notin $oldMemberNames }
                # Find removed members
                $removedMembers = $oldMemberNames | Where-Object { $_ -notin $newMemberNames }
                # Find modified members (same name but different content)
                $modifiedMembers = @()
                foreach ($member in $newMembers) {
                    $oldMember = $oldMembers | Where-Object { $_.name -eq $member.name }
                    if ($oldMember -and $oldMember.InnerXml -ne $member.InnerXml) {
                        $modifiedMembers += $member.name
                    }
                }
                
                if ($addedMembers.Count -gt 0 -or $removedMembers.Count -gt 0 -or $modifiedMembers.Count -gt 0) {
                    $HasChanges = $true
                    $changeEntry = @{
                        File = $newXml.Name
                        Added = $addedMembers
                        Removed = $removedMembers
                        Modified = $modifiedMembers
                    }
                    $ChangelogEntries += $changeEntry
                    
                    Write-Host "    → Added: $($addedMembers.Count) | Removed: $($removedMembers.Count) | Modified: $($modifiedMembers.Count)" -ForegroundColor Gray
                }
            }
            catch {
                Write-Host "    ⚠ Could not parse XML for detailed comparison" -ForegroundColor Yellow
                $HasChanges = $true
                $ChangelogEntries += @{ File = $newXml.Name; Message = "File changed (details unavailable)" }
            }
        }
    } else {
        # New file
        Write-Host "  • New file: $($newXml.Name)" -ForegroundColor Green
        $HasChanges = $true
        $ChangelogEntries += @{ File = $newXml.Name; Message = "New documentation file added" }
    }
}

# Check for removed files
if (Test-Path $LibrariesDest) {
    $OldXmlFiles = Get-ChildItem -Path $LibrariesDest -Filter "*.xml"
    foreach ($oldXml in $OldXmlFiles) {
        $newExists = $NewXmlFiles | Where-Object { $_.Name -eq $oldXml.Name }
        if (-not $newExists) {
            Write-Host "  • Removed file: $($oldXml.Name)" -ForegroundColor Red
            $HasChanges = $true
            $ChangelogEntries += @{ File = $oldXml.Name; Message = "Documentation file removed" }
        }
    }
}

if (-not $HasChanges) {
    Write-Host "  ✓ No changes detected in XML documentation" -ForegroundColor Green
}

Write-Host ""

# ============================================
# 2. Deploy Game Libraries
# ============================================
Write-Host "Deploying game libraries..." -ForegroundColor Yellow

if (Test-Path $LibrariesDest) {
    Write-Host "Removing existing Libraries folder..." -ForegroundColor Gray
    $retryCount = 0
    $maxRetries = 3
    while ($retryCount -lt $maxRetries) {
        try {
            Remove-Item $LibrariesDest -Recurse -Force -ErrorAction Stop
            break
        }
        catch {
            $retryCount++
            if ($retryCount -ge $maxRetries) {
                Write-Host ""
                Write-Host "ERROR: Cannot remove Libraries folder - files are in use!" -ForegroundColor Red
                Write-Host ""
                Write-Host "Please close the following applications and try again:" -ForegroundColor Yellow
                Write-Host "  • Godot Editor" -ForegroundColor Yellow
                Write-Host "  • Any RealmForge instances" -ForegroundColor Yellow
                Write-Host "  • Any applications referencing the game DLLs" -ForegroundColor Yellow
                Write-Host ""
                
                # Try to identify locked processes
                $godotProcesses = Get-Process | Where-Object { $_.ProcessName -match 'Godot' }
                if ($godotProcesses) {
                    Write-Host "Detected Godot processes:" -ForegroundColor Cyan
                    $godotProcesses | ForEach-Object { Write-Host "  • PID $($_.Id): $($_.ProcessName)" -ForegroundColor Gray }
                    Write-Host ""
                }
                
                exit 1
            }
            Write-Host "Retrying removal... ($retryCount/$maxRetries)" -ForegroundColor Yellow
            Start-Sleep -Seconds 1
        }
    }
}

Copy-Item -Path $LibrariesSource -Destination $LibrariesDest -Recurse -Force

# Copy XML documentation files to Libraries root for IntelliSense (need explicit copy after folder creation)
Write-Host "Copying XML documentation files..." -ForegroundColor Gray
$XmlFiles = Get-ChildItem -Path $LibrariesSource -Filter "*.xml"
if ($XmlFiles.Count -eq 0) {
    Write-Host "  ⚠ No XML files found in package!" -ForegroundColor Yellow
} else {
    foreach ($xmlFile in $XmlFiles) {
        try {
            Copy-Item -Path $xmlFile.FullName -Destination $LibrariesDest -Force -ErrorAction Stop
            Write-Host "  → Copied $($xmlFile.Name)" -ForegroundColor Gray
        }
        catch {
            Write-Host "  ✗ Failed to copy $($xmlFile.Name): $_" -ForegroundColor Red
        }
    }
}

$DllCount = (Get-ChildItem -Path $LibrariesDest -Recurse -Filter "*.dll").Count
$XmlCount = (Get-ChildItem -Path $LibrariesDest -Filter "*.xml").Count
$PdbCount = (Get-ChildItem -Path $LibrariesDest -Recurse -Filter "*.pdb").Count

Write-Host "✓ Deployed $DllCount DLL files" -ForegroundColor Green
if ($XmlCount -gt 0) {
    Write-Host "✓ Deployed $XmlCount XML documentation files (IntelliSense support)" -ForegroundColor Green
} else {
    Write-Host "⚠ No XML documentation files deployed" -ForegroundColor Yellow
}
if ($PdbCount -gt 0) {
    Write-Host "✓ Deployed $PdbCount PDB symbol files (debugging support)" -ForegroundColor Green
}
Write-Host ""
Write-Host ""

# ============================================
# 2. Deploy JSON Data
# ============================================
Write-Host "Deploying JSON data..." -ForegroundColor Yellow

$JsonSource = Join-Path $PackageRoot "Data\Json"
$JsonDest = Join-Path $GodotProjectPath "Data\Json"

if (Test-Path $JsonDest) {
    Remove-Item $JsonDest -Recurse -Force
}

Copy-Item -Path $JsonSource -Destination $JsonDest -Recurse -Force

$JsonFileCount = (Get-ChildItem -Path $JsonDest -Recurse -Filter "*.json").Count
$CbconfigCount = (Get-ChildItem -Path $JsonDest -Recurse -Filter "*.cbconfig.json").Count

Write-Host "✓ Deployed $JsonFileCount JSON data files" -ForegroundColor Green
if ($CbconfigCount -gt 0) {
    Write-Host "  → Including $CbconfigCount RealmForge config files" -ForegroundColor Gray
}
Write-Host ""

# ============================================
# 3. Deploy RealmForge (Optional)
# ============================================
$DeployRealmForge = Read-Host "Deploy RealmForge to Godot project? (y/N)"

if ($DeployRealmForge -eq "y" -or $DeployRealmForge -eq "Y") {
    Write-Host "Deploying RealmForge..." -ForegroundColor Yellow
    
    $RealmForgeSource = Join-Path $PackageRoot "RealmForge"
    $RealmForgeDest = Join-Path $GodotProjectPath "Tools\RealmForge"
    
    if (Test-Path $RealmForgeDest) {
        Remove-Item $RealmForgeDest -Recurse -Force
    }
    
    New-Item -ItemType Directory -Path $RealmForgeDest -Force | Out-Null
    Copy-Item -Path "$RealmForgeSource\*" -Destination $RealmForgeDest -Recurse -Force

    $ExeCount = (Get-ChildItem -Path $RealmForgeDest -Filter "*.exe" -Recurse).Count
    $DllCount = (Get-ChildItem -Path $RealmForgeDest -Filter "*.dll" -Recurse).Count
    
    Write-Host "✓ RealmForge deployed to Tools/RealmForge" -ForegroundColor Green
    Write-Host "  → $ExeCount executable, $DllCount DLLs" -ForegroundColor Gray
    Write-Host ""
}

# ============================================
# 4. Deploy Release Notes & API Documentation
# ============================================
Write-Host "Deploying documentation..." -ForegroundColor Yellow

# Copy release-notes.md from package
$ReleaseNotesSource = Join-Path $PackageRoot "release-notes.md"
$ReleaseNotesDest = Join-Path $GodotProjectPath "RELEASE_NOTES.md"

if (Test-Path $ReleaseNotesSource) {
    Copy-Item -Path $ReleaseNotesSource -Destination $ReleaseNotesDest -Force
    Write-Host "✓ Release notes deployed (RELEASE_NOTES.md)" -ForegroundColor Green
    Write-Host "  → Contains git diff since last tag and API changes" -ForegroundColor Gray
} else {
    Write-Host "⚠ No release-notes.md found in package" -ForegroundColor Yellow
}

# Copy API_SPECIFICATION.md from package
$ApiDocSource = Join-Path $PackageRoot "API_SPECIFICATION.md"
$ApiDocDest = Join-Path $GodotProjectPath "API_SPECIFICATION.md"

if (Test-Path $ApiDocSource) {
    Copy-Item -Path $ApiDocSource -Destination $ApiDocDest -Force
    Write-Host "✓ API documentation deployed (API_SPECIFICATION.md)" -ForegroundColor Green
    Write-Host "  → Complete API reference and integration guide" -ForegroundColor Gray
} else {
    Write-Host "⚠ No API_SPECIFICATION.md found in package" -ForegroundColor Yellow
}

Write-Host "Generating deployment info..." -ForegroundColor Yellow

$DeploymentInfo = @{
    DeploymentDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    SourcePackage = $PackageRoot
    GodotProject = $GodotProjectPath
    LibrariesDeployed = $DllCount
    JsonFilesDeployed = $JsonFileCount
}

$DeploymentJson = $DeploymentInfo | ConvertTo-Json -Depth 10
$DeploymentPath = Join-Path $GodotProjectPath ".deployment-info.json"
Set-Content -Path $DeploymentPath -Value $DeploymentJson

Write-Host "Deployment info saved" -ForegroundColor Green

# Generate Changelog if changes detected
if ($HasChanges -and $ChangelogEntries.Count -gt 0) {
    Write-Host "Generating changelog..." -ForegroundColor Yellow
    
    $ChangelogPath = Join-Path $GodotProjectPath "CHANGELOG_API.md"
    $ChangelogExists = Test-Path $ChangelogPath
    
    # Build changelog entry
    $ChangelogHeader = @"
## Deployment - $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

"@
    
    $ChangelogBody = ""
    
    foreach ($entry in $ChangelogEntries) {
        if ($entry.Message) {
            # Simple message entry
            $ChangelogBody += "### $($entry.File)`n"
            $ChangelogBody += "$($entry.Message)`n`n"
        } else {
            # Detailed API changes
            $ChangelogBody += "### $($entry.File)`n`n"
            
            if ($entry.Added -and $entry.Added.Count -gt 0) {
                $ChangelogBody += "**Added ($($entry.Added.Count)):**`n"
                foreach ($member in $entry.Added) {
                    $memberName = $member -replace "^[TFMPE]:", "" -replace "[\(\[].*", ""
                    $memberType = switch -Regex ($member) {
                        "^T:" { "Type" }
                        "^F:" { "Field" }
                        "^M:" { "Method" }
                        "^P:" { "Property" }
                        "^E:" { "Event" }
                        default { "Member" }
                    }
                    $ChangelogBody += "- [$memberType] ``$memberName```n"
                }
                $ChangelogBody += "`n"
            }
            
            if ($entry.Removed -and $entry.Removed.Count -gt 0) {
                $ChangelogBody += "**Removed ($($entry.Removed.Count)):**`n"
                foreach ($member in $entry.Removed) {
                    $memberName = $member -replace "^[TFMPE]:", "" -replace "[\(\[].*", ""
                    $memberType = switch -Regex ($member) {
                        "^T:" { "Type" }
                        "^F:" { "Field" }
                        "^M:" { "Method" }
                        "^P:" { "Property" }
                        "^E:" { "Event" }
                        default { "Member" }
                    }
                    $ChangelogBody += "- ~~[$memberType] ``$memberName``~~`n"
                }
                $ChangelogBody += "`n"
            }
            
            if ($entry.Modified -and $entry.Modified.Count -gt 0) {
                $ChangelogBody += "**Modified ($($entry.Modified.Count)):**`n"
                foreach ($member in $entry.Modified) {
                    $memberName = $member -replace "^[TFMPE]:", "" -replace "[\(\[].*", ""
                    $memberType = switch -Regex ($member) {
                        "^T:" { "Type" }
                        "^F:" { "Field" }
                        "^M:" { "Method" }
                        "^P:" { "Property" }
                        "^E:" { "Event" }
                        default { "Member" }
                    }
                    $ChangelogBody += "- [$memberType] ``$memberName`` (documentation updated)`n"
                }
                $ChangelogBody += "`n"
            }
        }
    }
    
    # Prepend to existing changelog or create new
    if ($ChangelogExists) {
        $ExistingChangelog = Get-Content $ChangelogPath -Raw
        $NewChangelog = $ChangelogHeader + $ChangelogBody + "`n---`n`n" + $ExistingChangelog
    } else {
        $NewChangelog = "# API Changelog`n`nAutomatically generated from XML documentation differences.`n`n---`n`n" + $ChangelogHeader + $ChangelogBody
    }
    
    Set-Content -Path $ChangelogPath -Value $NewChangelog
    
    Write-Host "✓ Changelog generated: CHANGELOG_API.md" -ForegroundColor Green
    Write-Host "  → $($ChangelogEntries.Count) file(s) with changes documented" -ForegroundColor Gray
} else {
    Write-Host "No API changes detected - changelog not updated" -ForegroundColor Gray
}

Write-Host ""

# ============================================
# 5. Summary
# ============================================
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Deployed to: " -NoNewline -ForegroundColor Yellow
Write-Host $GodotProjectPath -ForegroundColor White
Write-Host ""
Write-Host "Components:" -ForegroundColor Yellow
Write-Host "  - $DllCount DLL files in Libraries" -ForegroundColor Green
Write-Host "  - $JsonFileCount JSON files in Data/Json" -ForegroundColor Green
if ($DeployRealmForge -eq "y" -or $DeployRealmForge -eq "Y") {
    Write-Host "  - RealmForge in Tools/RealmForge" -ForegroundColor Green
}
if ($HasChanges) {
    Write-Host "  - API Changelog updated (CHANGELOG_API.md)" -ForegroundColor Cyan
}
if (Test-Path $ReleaseNotesDest) {
    Write-Host "  - Release Notes with Git Diff (RELEASE_NOTES.md)" -ForegroundColor Cyan
}
if (Test-Path $ApiDocDest) {
    Write-Host "  - API Documentation (API_SPECIFICATION.md)" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Open Godot project" -ForegroundColor Gray
Write-Host "  2. Build CSharp solution in Godot (Ctrl+B)" -ForegroundColor Gray
Write-Host "  3. Reference game DLLs in your GDScript or CSharp code" -ForegroundColor Gray
Write-Host ""
