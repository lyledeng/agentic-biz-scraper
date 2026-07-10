#Requires -Version 7.0
# CI Validation: Verify all Liquibase SQL changesets have --comment: attributes
# Exit 0 = pass, Exit 1 = missing comments found

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$changelogDir = Join-Path $repoRoot 'deploy' 'liquibase' 'changelog' 'ddl'

$missing = @()

Get-ChildItem $changelogDir -Filter '*.sql' | ForEach-Object {
    $lines = Get-Content $_.FullName
    $hasChangeset = $false
    $hasComment = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '--\s*changeset\s+') {
            if ($hasChangeset -and -not $hasComment) {
                $missing += "$($_.Name): changeset at line $changesetLine missing --comment:"
            }
            $hasChangeset = $true
            $hasComment = $false
            $changesetLine = $i + 1
        }
        if ($lines[$i] -match '--comment[:  ]') {
            $hasComment = $true
        }
    }

    if ($hasChangeset -and -not $hasComment) {
        $missing += "$($_.Name): changeset at line $changesetLine missing --comment:"
    }
}

if ($missing.Count -gt 0) {
    Write-Host "FAIL: $($missing.Count) changeset(s) missing --comment: attribute:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    exit 1
}

Write-Host "PASS: All Liquibase changesets have --comment: attributes." -ForegroundColor Green
exit 0
