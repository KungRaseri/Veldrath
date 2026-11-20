#!/usr/bin/env pwsh
# Generate a timestamped code coverage report.
#
# The reporter lives in its own repository (codecov/codecov-report) and is pinned to
# a release tag here, so there is no global tool to install and no ReportGenerator PRO
# license involved. It is fetched on demand into .coverage-report/<ref>/ (gitignored).
#
# Usage: .\scripts\generate-coverage-report.ps1
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$resultsRoot = Join-Path $repoRoot "coverage-results"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$reportDir = Join-Path $resultsRoot "report\$timestamp"

$toolRepo = if ($env:COVERAGE_REPORT_REPO) { $env:COVERAGE_REPORT_REPO } else { "https://git.kungraseri.cloud/codecov/codecov-report.git" }
$toolRef = if ($env:COVERAGE_REPORT_REF) { $env:COVERAGE_REPORT_REF } else { "v1.0.4" }
$toolDir = if ($env:COVERAGE_REPORT_DIR) { $env:COVERAGE_REPORT_DIR } else { Join-Path $repoRoot ".coverage-report\$toolRef" }
$project = Join-Path $toolDir "src\CoverageReport\CoverageReport.csproj"

if (-not (Test-Path $project)) {
    Write-Host "Fetching coverage reporter $toolRef from $toolRepo ..." -ForegroundColor Cyan
    if (Test-Path $toolDir) {
        Remove-Item $toolDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $toolDir) | Out-Null
    git -c advice.detachedHead=false clone --quiet --depth 1 --branch $toolRef $toolRepo $toolDir
}

Write-Host "Generating coverage report in: $reportDir" -ForegroundColor Cyan

# Find the most recent coverage file
$coverageFile = Get-ChildItem -Path $resultsRoot -Filter "coverage.cobertura.xml" -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $coverageFile) {
    Write-Host "Error: No coverage file found. Run dotnet test with --collect 'XPlat Code Coverage' first." -ForegroundColor Red
    exit 1
}

Write-Host "Using coverage file: $($coverageFile.FullName)" -ForegroundColor Green

dotnet run --project $project --configuration Release -- `
    "-reports:$($coverageFile.FullName)" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;Badges;TextSummary"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nError generating coverage report (exit code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nCoverage report generated successfully!" -ForegroundColor Green
Write-Host "Report location: $reportDir\index.html" -ForegroundColor Cyan

# Also refresh a 'latest' copy for convenience
$latestDir = Join-Path $resultsRoot "report\latest"
if (Test-Path $latestDir) {
    Remove-Item $latestDir -Recurse -Force
}

Copy-Item -Path $reportDir -Destination $latestDir -Recurse
Write-Host "Latest report also available at: $latestDir\index.html" -ForegroundColor Cyan

$summaryFile = Join-Path $reportDir "Summary.txt"
if (Test-Path $summaryFile) {
    Write-Host "`n=== Coverage Summary ===" -ForegroundColor Yellow
    Get-Content $summaryFile | Select-Object -First 20
}
