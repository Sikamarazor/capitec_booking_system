# Run API and Angular dev server without Docker. Stop with Ctrl+C.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET SDK 10 not found (https://dotnet.microsoft.com/download)' }
if (-not (Get-Command node -ErrorAction SilentlyContinue))   { throw 'Node.js 22.12+ not found (https://nodejs.org)' }

$hasSecret = $false
if ($env:ConnectionStrings__BranchBooking) { $hasSecret = $true }
else {
    $secrets = & dotnet user-secrets list --project src/BranchBooking.Api 2>$null
    if ($secrets -match 'ConnectionStrings:BranchBooking') { $hasSecret = $true }
}
if (-not $hasSecret) {
    throw 'No database connection string configured. See docs/engineering/local-postgres.md'
}

Write-Host 'Starting API on http://localhost:5080 ...'
$api = Start-Process dotnet -ArgumentList 'run', '--project', 'src/BranchBooking.Api', '--launch-profile', 'http' -PassThru -NoNewWindow

try {
    Write-Host 'Starting web on http://localhost:4200 ...'
    Push-Location frontend/branchbooking-web
    npm ci
    npm start
}
finally {
    Pop-Location
    if (-not $api.HasExited) {
        if ($IsWindows -or $env:OS -eq 'Windows_NT') { & taskkill /PID $api.Id /T /F | Out-Null }
        else { Stop-Process -Id $api.Id -Force }
    }
}
