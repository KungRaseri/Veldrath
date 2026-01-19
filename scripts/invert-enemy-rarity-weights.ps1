# Invert enemy rarityWeight values to match spawn selection semantics
# Current: higher value = rarer (being used as rarity score)
# Target: higher value = more common (spawn weight)
# Transformation: newValue = 100 - oldValue (or similar inversion)

$enemyDir = "c:\code\console-game\RealmEngine.Data\Data\Json\enemies"
$catalogFiles = Get-ChildItem -Path $enemyDir -Recurse -Filter "catalog.json"

foreach ($file in $catalogFiles) {
    Write-Host "Processing: $($file.FullName)" -ForegroundColor Cyan
    
    $content = Get-Content $file.FullName -Raw
    $modified = $false
    
    # Find all rarityWeight values and invert them
    # Using regex to find rarityWeight: <number>
    $pattern = '("rarityWeight":\s*)(\d+)'
    
    $newContent = [regex]::Replace($content, $pattern, {
        param($match)
        $prefix = $match.Groups[1].Value
        $oldValue = [int]$match.Groups[2].Value
        
        # Invert: 100 becomes 10, 10 becomes 90, 50 stays 50
        # Formula: newValue = 100 - oldValue + 10 (to keep minimum at 10)
        $newValue = 110 - $oldValue
        if ($newValue -lt 1) { $newValue = 1 }
        if ($newValue -gt 100) { $newValue = 100 }
        
        Write-Host "  $oldValue -> $newValue" -ForegroundColor Yellow
        $modified = $true
        return "$prefix$newValue"
    })
    
    if ($modified) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "  [UPDATED]" -ForegroundColor Green
    } else {
        Write-Host "  (no changes)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Done! Enemy rarityWeights inverted." -ForegroundColor Green
