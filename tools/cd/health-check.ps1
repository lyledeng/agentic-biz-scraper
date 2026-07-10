<#
.SYNOPSIS
    Verifies the deployed IIS application responds at the public URL after
    the app pool has restarted.
.PARAMETER HealthCheckUrl
    The URL to probe (e.g. https://localhost/mvpoc/bizscraper-api/ready).
#>
param(
    [Parameter(Mandatory)] [string] $HealthCheckUrl
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1: enable TLS 1.2 and bypass certificate validation
if ($PSVersionTable.PSVersion.Major -lt 7) {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    Add-Type @"
        using System.Net;
        using System.Net.Security;
        using System.Security.Cryptography.X509Certificates;
        public class TrustAllCerts {
            public static void Ignore() {
                ServicePointManager.ServerCertificateValidationCallback =
                    delegate { return true; };
            }
        }
"@
    [TrustAllCerts]::Ignore()
}

$maxAttempts = 10
$delaySeconds = 5

for ($i = 1; $i -le $maxAttempts; $i++) {
    Write-Host "Health check attempt $i/$maxAttempts : $HealthCheckUrl"
    try {
        $invokeArgs = @{
            Uri            = $HealthCheckUrl
            UseBasicParsing = $true
            TimeoutSec     = 10
            ErrorAction    = 'Stop'
        }
        if ($PSVersionTable.PSVersion.Major -ge 7) {
            $invokeArgs['SkipCertificateCheck'] = $true
        }
        $response = Invoke-WebRequest @invokeArgs
        if ($response.StatusCode -eq 200) {
            Write-Host "Health check passed (HTTP $($response.StatusCode))."
            return
        }
        Write-Warning "Unexpected status: $($response.StatusCode)"
    }
    catch {
        Write-Warning "Attempt $i failed: $_"
    }
    if ($i -lt $maxAttempts) {
        Start-Sleep -Seconds $delaySeconds
    }
}

throw "Health check failed after $maxAttempts attempts against $HealthCheckUrl"
