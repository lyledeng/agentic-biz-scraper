<#
.SYNOPSIS
    Cleans the artifact download path to prevent stale files from prior runs.
.PARAMETER ArtifactDownloadPath
    Absolute path where the pipeline artifact will be downloaded.
#>
param(
    [Parameter(Mandatory)] [string] $ArtifactDownloadPath
)

$ErrorActionPreference = 'Stop'

if (Test-Path $ArtifactDownloadPath) {
    Write-Host "Removing stale artifact path: $ArtifactDownloadPath"
    Remove-Item -Recurse -Force $ArtifactDownloadPath
}

New-Item -ItemType Directory -Path $ArtifactDownloadPath -Force | Out-Null
Write-Host "Prepared clean artifact path: $ArtifactDownloadPath"
