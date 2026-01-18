# Fix trailing commas in catalog.json files

$dataPath = Join-Path $PSScriptRoot ".." "RealmEngine.Data" "Data" "Json"
$catalogFiles = Get-ChildItem -Path $dataPath -Recurse -Filter "catalog.json"

$fixed = 0
$errors = 0

foreach ($file in $catalogFiles) {
    try {
        $content = Get-Content $file.FullName -Raw
        
        # Fix trailing commas before closing braces/brackets
        $originalContent = $content
        $content = $content -replace ',(\s*[}\]])', '$1'
        
        if ($content -ne $originalContent) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            $relativePath = $file.FullName.Replace($dataPath, "").TrimStart("\")
            Write-Host "  Fixed: $relativePath" -ForegroundColor Green
            $fixed++
        }
    }
    catch {
        $relativePath = $file.FullName.Replace($dataPath, "").TrimStart("\")
        Write-Host "  Error fixing ${relativePath}: $_" -ForegroundColor Red
        $errors++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Fixed: $fixed files" -ForegroundColor Green
if ($errors -gt 0) {
    Write-Host "  Errors: $errors files" -ForegroundColor Red
} else {
    Write-Host "  Errors: $errors files" -ForegroundColor Green
}
