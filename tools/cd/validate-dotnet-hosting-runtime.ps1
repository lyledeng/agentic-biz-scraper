<#
.SYNOPSIS
    Validates that the target VM has the ASP.NET Core hosting bundle and shared
    runtime required for a framework-dependent deployment.
.PARAMETER RequiredVersionPrefix
    Major.Minor version prefix to match (e.g. "10.0").
#>
param(
    [Parameter(Mandatory)] [string] $RequiredVersionPrefix
)

$ErrorActionPreference = 'Stop'

# Check ASP.NET Core runtime (shared framework)
$runtimes = & dotnet --list-runtimes 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet CLI not found. Install the .NET $RequiredVersionPrefix hosting bundle."
}

$aspNetRuntime = $runtimes | Where-Object { $_ -match "Microsoft\.AspNetCore\.App\s+$([regex]::Escape($RequiredVersionPrefix))" }
if (-not $aspNetRuntime) {
    Write-Error "ASP.NET Core runtime $RequiredVersionPrefix not found. Installed runtimes:"
    $runtimes | ForEach-Object { Write-Host "  $_" }
    throw "Install the .NET $RequiredVersionPrefix hosting bundle from https://dotnet.microsoft.com/download"
}

Write-Host "Found matching runtime: $($aspNetRuntime | Select-Object -First 1)"

# Check IIS ASP.NET Core Module (ANCM)
$ancmPath = "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (-not (Test-Path $ancmPath)) {
    throw "ASP.NET Core Module V2 not found at $ancmPath. Install the hosting bundle."
}

Write-Host "ASP.NET Core Module V2 found. Runtime validation passed."
