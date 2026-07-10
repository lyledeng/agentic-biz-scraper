#Requires -Version 7.0
# CI Validation: Verify specs/current-specs/ global snapshot exists with required files
# Exit 0 = pass, Exit 1 = missing files found

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$specsDir = Join-Path $repoRoot 'specs' 'current-specs'

$requiredFiles = @(
    'spec.md',
    'data-model.md',
    (Join-Path 'contracts' 'execute-script.md'),
    (Join-Path 'contracts' 'definitions.md'),
    (Join-Path 'contracts' 'document-proxy.md'),
    (Join-Path 'contracts' 'germany-hardcopy.md'),
    (Join-Path 'contracts' 'health-check.md'),
    (Join-Path 'contracts' 'deprecated-business-search.md'),
    (Join-Path 'contracts' 'deprecated-entity-details.md'),
    (Join-Path 'contracts' 'deprecated-germany-search.md')
)

$missing = @()

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $specsDir $file
    if (-not (Test-Path $fullPath)) {
        $missing += "specs/current-specs/$file"
    }
}

if ($missing.Count -gt 0) {
    Write-Host "FAIL: $($missing.Count) required file(s) missing from specs/current-specs/:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    exit 1
}

Write-Host "PASS: Global specification snapshot contains all required files." -ForegroundColor Green
exit 0
