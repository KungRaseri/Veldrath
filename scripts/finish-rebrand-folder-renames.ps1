# finish-rebrand-folder-renames.ps1
#
# Completes the RealmUnbound -> Veldrath folder renames.
# VS Code must be CLOSED before running this script.
#
# Usage (from a standalone PowerShell terminal, not the VS Code integrated one):
#   cd C:\code\RealmEngine
#   powershell -ExecutionPolicy Bypass -File .\scripts\finish-rebrand-folder-renames.ps1

$root = "C:\code\RealmEngine"

$renames = @(
    @{ From = "RealmUnbound.Assets";        To = "Veldrath.Assets"        }
    @{ From = "RealmUnbound.Assets.Tests";  To = "Veldrath.Assets.Tests"  }
    @{ From = "RealmUnbound.Client";        To = "Veldrath.Client"        }
    @{ From = "RealmUnbound.Client.Tests";  To = "Veldrath.Client.Tests"  }
    @{ From = "RealmUnbound.Contracts";     To = "Veldrath.Contracts"     }
    @{ From = "RealmUnbound.Discord";       To = "Veldrath.Discord"       }
    @{ From = "RealmUnbound.Discord.Tests"; To = "Veldrath.Discord.Tests" }
    @{ From = "RealmUnbound.Server";        To = "Veldrath.Server"        }
)

$allGood = $true
foreach ($r in $renames) {
    $src = Join-Path $root $r.From
    $dst = Join-Path $root $r.To

    if (-not (Test-Path $src)) {
        Write-Host "SKIP (already renamed or missing): $($r.From)"
        continue
    }

    if (Test-Path $dst) {
        Write-Host "SKIP (destination already exists): $($r.To)"
        continue
    }

    try {
        [System.IO.Directory]::Move($src, $dst)
        Write-Host "OK  $($r.From)  ->  $($r.To)"
    } catch {
        Write-Host "FAIL $($r.From): $_"
        $allGood = $false
    }
}

Write-Host ""
if ($allGood) {
    Write-Host "All renames completed. You can now reopen VS Code."
} else {
    Write-Host "Some renames failed (see above). Close any processes locking those folders and retry."
}
