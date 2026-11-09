# Usage: scripts/verify-foundation.ps1 [-ApiUrl http://localhost:8080] [-WebUrl http://localhost:8080]
param(
    [string]$ApiUrl = 'http://localhost:8080',
    [string]$WebUrl = 'http://localhost:8080'
)
$fail = 0

function Test-Status($name, $url, $expected) {
    try { $code = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10).StatusCode }
    catch { $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 } }
    if ($code -eq $expected) { Write-Host "PASS  $name ($url) -> $code" }
    else { Write-Host "FAIL  $name ($url) -> $code, expected $expected"; $script:fail = 1 }
}

Test-Status 'API liveness'  "$ApiUrl/api/v1/health"       200
Test-Status 'API readiness' "$ApiUrl/api/v1/health/ready" 200
Test-Status 'Web index'     "$WebUrl/"                    200

$index = (Invoke-WebRequest -Uri "$WebUrl/" -UseBasicParsing -TimeoutSec 10).Content
if ($index -match 'bb-root') { Write-Host 'PASS  Web serves the Angular shell' } else { Write-Host 'FAIL  Web response does not contain bb-root'; $fail = 1 }

$viaWeb = (Invoke-WebRequest -Uri "$WebUrl/api/v1/health" -UseBasicParsing -TimeoutSec 10).Content
if ($viaWeb -match '"status":"ok"') { Write-Host 'PASS  Web can reach the API' } else { Write-Host 'FAIL  Web cannot reach the API through its proxy'; $fail = 1 }

exit $fail
