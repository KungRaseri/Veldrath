#!/usr/bin/env pwsh
# Generate code coverage report with timestamp
# Usage: .\scripts\generate-coverage-report.ps1

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportDir = "coverage-results\report\$timestamp"

Write-Host "Generating coverage report in: $reportDir" -ForegroundColor Cyan

# Find the most recent coverage file
$coverageFile = Get-ChildItem -Path "coverage-results" -Filter "coverage.cobertura.xml" -Recurse | 
Sort-Object LastWriteTime -Descending | 
Select-Object -First 1

if (-not $coverageFile) {
    Write-Host "Error: No coverage file found. Run 'dotnet test --collect:XPlat Code Coverage' first." -ForegroundColor Red
    exit 1
}

Write-Host "Using coverage file: $($coverageFile.FullName)" -ForegroundColor Green

# Generate HTML report
reportgenerator `
    "-reports:$($coverageFile.FullName)" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;Badges;TextSummary"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nCoverage report generated successfully!" -ForegroundColor Green
    Write-Host "Report location: $reportDir\index.html" -ForegroundColor Cyan
    
    # Also create a 'latest' symlink/copy for convenience
    $latestDir = "coverage-results\report\latest"
    if (Test-Path $latestDir) {
        Remove-Item $latestDir -Recurse -Force
    }
    Copy-Item -Path $reportDir -Destination $latestDir -Recurse
    Write-Host "Latest report also available at: $latestDir\index.html" -ForegroundColor Cyan
    
    # Display summary
    $summaryFile = Join-Path $reportDir "Summary.txt"
    if (Test-Path $summaryFile) {
        Write-Host "`n=== Coverage Summary ===" -ForegroundColor Yellow
        Get-Content $summaryFile | Select-Object -First 20
    }
}
else {
    Write-Host "`nError generating coverage report (exit code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
