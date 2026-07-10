<#
.SYNOPSIS
    Deploys the published IIS artifact: ensures the IIS application and app pool
    exist, syncs files to the physical path, and recycles the app pool.
.PARAMETER ArtifactRoot
    Path to the downloaded artifact containing published binaries.
.PARAMETER ExpectedAppDll
    DLL name to validate the artifact contains the expected application.
.PARAMETER IisWebsiteName
    Parent IIS website name (e.g. "Default Web Site").
.PARAMETER IisApplicationPath
    Virtual path under the website (e.g. "/mvpoc/bizscraper-api").
.PARAMETER IisAppPoolName
    Name of the IIS application pool.
.PARAMETER IisPhysicalPath
    Target physical path on disk for the application files.
#>
param(
    [Parameter(Mandatory)] [string] $ArtifactRoot,
    [Parameter(Mandatory)] [string] $ExpectedAppDll,
    [Parameter(Mandatory)] [string] $IisWebsiteName,
    [Parameter(Mandatory)] [string] $IisApplicationPath,
    [Parameter(Mandatory)] [string] $IisAppPoolName,
    [Parameter(Mandatory)] [string] $IisPhysicalPath,
    [string] $CloudStorageConnectionString = ''
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration -ErrorAction Stop

# Validate artifact
$dllPath = Join-Path $ArtifactRoot $ExpectedAppDll
if (-not (Test-Path $dllPath)) {
    throw "Expected DLL not found at $dllPath. Artifact may be corrupt or incomplete."
}
Write-Host "Artifact validated: $dllPath"

# Ensure app pool exists
if (-not (Test-Path "IIS:\AppPools\$IisAppPoolName")) {
    Write-Host "Creating app pool: $IisAppPoolName"
    New-WebAppPool -Name $IisAppPoolName
    Set-ItemProperty "IIS:\AppPools\$IisAppPoolName" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$IisAppPoolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
}

# Stop app pool before file sync and wait for worker process to exit
if ((Get-WebAppPoolState -Name $IisAppPoolName).Value -eq 'Started') {
    Write-Host "Stopping app pool: $IisAppPoolName"
    Stop-WebAppPool -Name $IisAppPoolName
    $timeout = 30
    $elapsed = 0
    while ((Get-WebAppPoolState -Name $IisAppPoolName).Value -ne 'Stopped' -and $elapsed -lt $timeout) {
        Start-Sleep -Seconds 1
        $elapsed++
    }
    if ((Get-WebAppPoolState -Name $IisAppPoolName).Value -ne 'Stopped') {
        Write-Warning "App pool did not stop within ${timeout}s - forcing worker process termination."
        $workerPids = (Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'" |
            Where-Object { $_.CommandLine -match $IisAppPoolName }).ProcessId
        foreach ($pid in $workerPids) {
            Write-Host "Killing w3wp.exe PID $pid"
            Stop-Process -Id $pid -Force
        }
        Start-Sleep -Seconds 2
    }
    Write-Host "App pool stopped."
}

# Ensure physical path exists
if (-not (Test-Path $IisPhysicalPath)) {
    New-Item -ItemType Directory -Path $IisPhysicalPath -Force | Out-Null
}

# Sync files (robocopy /MIR mirrors source to destination)
Write-Host "Syncing files from $ArtifactRoot to $IisPhysicalPath"
$robocopyArgs = @($ArtifactRoot, $IisPhysicalPath, '/MIR', '/NP', '/NDL', '/NFL')
& robocopy @robocopyArgs
# robocopy exit codes 0-7 are success/informational
if ($LASTEXITCODE -gt 7) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}
$LASTEXITCODE = 0

# Ensure IIS application exists
$appPath = "IIS:\Sites\$IisWebsiteName$IisApplicationPath"
if (-not (Test-Path $appPath)) {
    Write-Host "Creating IIS application: $IisWebsiteName$IisApplicationPath"
    New-WebApplication -Site $IisWebsiteName -Name ($IisApplicationPath.TrimStart('/')) `
        -PhysicalPath $IisPhysicalPath -ApplicationPool $IisAppPoolName
} else {
    Set-ItemProperty $appPath -Name physicalPath -Value $IisPhysicalPath
    Set-ItemProperty $appPath -Name applicationPool -Value $IisAppPoolName
}

# Inject environment variables into web.config for IIS worker processes
$webConfigPath = Join-Path $IisPhysicalPath 'web.config'
if (Test-Path $webConfigPath) {
    [xml]$webConfig = Get-Content $webConfigPath
    $aspNetCore = $webConfig.SelectSingleNode('//aspNetCore')
    if ($aspNetCore) {
        $envVars = $aspNetCore.SelectSingleNode('environmentVariables')
        if (-not $envVars) {
            $envVars = $webConfig.CreateElement('environmentVariables')
            $aspNetCore.AppendChild($envVars) | Out-Null
        }

        $envSettings = @{
            'Playwright__BrowserChannel'   = 'msedge'
            'ConnectionStrings__AuditTrail' = 'Data Source=D:\data\bizscraper.db'
        }

        if ($CloudStorageConnectionString) {
            $envSettings['Playwright__Diagnostics__CloudStorage__Enabled'] = 'true'
            $envSettings['Playwright__Diagnostics__CloudStorage__ContainerName'] = 'playwright-assembly'
            $envSettings['Playwright__Diagnostics__CloudStorage__ConnectionString'] = $CloudStorageConnectionString
        }

        foreach ($kvp in $envSettings.GetEnumerator()) {
            $existing = $envVars.SelectSingleNode("environmentVariable[@name='$($kvp.Key)']")
            if ($existing) {
                $existing.SetAttribute('value', $kvp.Value)
            } else {
                $envVar = $webConfig.CreateElement('environmentVariable')
                $envVar.SetAttribute('name', $kvp.Key)
                $envVar.SetAttribute('value', $kvp.Value)
                $envVars.AppendChild($envVar) | Out-Null
            }
            Write-Host "web.config: $($kvp.Key) = $($kvp.Value)"
        }

        $webConfig.Save($webConfigPath)
        Write-Host "web.config updated."
    } else {
        Write-Warning 'No aspNetCore element found in web.config; skipping env var injection.'
    }
} else {
    Write-Warning "web.config not found at $webConfigPath - skipping env var injection."
}

# Start app pool
Write-Host "Starting app pool: $IisAppPoolName"
Start-WebAppPool -Name $IisAppPoolName

Write-Host "Deployment complete: $IisWebsiteName$IisApplicationPath => $IisPhysicalPath"
exit 0
