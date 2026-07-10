#Requires -Version 7.0
# CI Validation: Verify all public C# types have XML <summary> doc-comments
# Exit 0 = pass, Exit 1 = missing docs found

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$srcDir = Join-Path $repoRoot 'src' 'BizScraper.Api'

$missing = @()

Get-ChildItem $srcDir -Recurse -Filter '*.cs' | ForEach-Object {
    $lines = Get-Content $_.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'public\s+(sealed\s+|abstract\s+|static\s+)*(record|class|interface|enum)\s+(\w+)') {
            $typeName = $Matches[3]
            $hasDoc = $false
            for ($j = [Math]::Max(0, $i - 10); $j -lt $i; $j++) {
                if ($lines[$j] -match '///\s*<summary>') { $hasDoc = $true }
            }
            if (-not $hasDoc) {
                $rel = $_.FullName.Replace($repoRoot + [IO.Path]::DirectorySeparatorChar, '')
                $missing += "$rel : $typeName (line $($i + 1))"
            }
        }
    }
}

if ($missing.Count -gt 0) {
    Write-Host "FAIL: $($missing.Count) public type(s) missing XML doc-comments:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    exit 1
}

Write-Host "PASS: All public types have XML doc-comments." -ForegroundColor Green
exit 0
