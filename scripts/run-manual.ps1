# Run API, notification Worker and Angular without Docker. Stop with Ctrl+C.
param([switch]$Mailpit)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
. (Join-Path $PSScriptRoot 'manual-processes.ps1')

foreach ($tool in @('dotnet', 'node', 'npm')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool not found. Install .NET SDK 10 and Node.js 22.12+." }
}
if ($Mailpit -and -not (Get-Command mailpit -ErrorAction SilentlyContinue)) {
    throw 'Install the native Mailpit executable and add it to PATH. See README.md, Mailpit without Docker.'
}
foreach ($project in @('src/BranchBooking.Api', 'src/BranchBooking.Worker')) {
    if (-not $env:ConnectionStrings__BranchBooking) {
        $secrets = & dotnet user-secrets list --project $project 2>$null
        if (-not ($secrets -match 'ConnectionStrings:BranchBooking')) {
            throw "No database connection string for $project. Set ConnectionStrings__BranchBooking or configure each project's user-secrets."
        }
    }
}

# Report port conflicts before installing dependencies or launching any child processes.
$ports = @(@{ Name = 'API'; Port = 5080 }, @{ Name = 'Frontend'; Port = 4200 })
if ($Mailpit) { $ports += @(@{ Name = 'Mailpit SMTP'; Port = 1025 }, @{ Name = 'Mailpit inbox'; Port = 8025 }) }
Assert-ManualPortsAvailable -Ports $ports

# Apply the SMTP preset only to child processes and restore this PowerShell session afterwards.
$settings = @{
    Notifications__Channel = 'Smtp'
    Notifications__PublicBaseUrl = 'http://localhost:4200'
    Notifications__Smtp__Host = '127.0.0.1'
    Notifications__Smtp__Port = '1025'
    Notifications__Smtp__Security = 'None'
    Notifications__Smtp__Username = ''
    Notifications__Smtp__Password = ''
    Notifications__Smtp__SenderEmail = 'noreply@branchbooking.example'
    Notifications__Smtp__SenderName = 'BranchBooking'
}
$previous = @{}
$services = @()
$logDirectory = Join-Path (Get-Location) ('artifacts/manual-startup/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Write-Host "Startup logs: $logDirectory"
$webDirectory = Join-Path (Get-Location) 'frontend/branchbooking-web'

try {
    if ($Mailpit) {
        foreach ($key in $settings.Keys) {
            $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
        }
    }
    Push-Location $webDirectory
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }
    finally { Pop-Location }
    foreach ($project in @('src/BranchBooking.Api', 'src/BranchBooking.Worker')) {
        dotnet build $project
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $project." }
    }

    if ($Mailpit) {
        Write-Host 'Starting Mailpit: inbox http://localhost:8025, SMTP 127.0.0.1:1025 ...'
        $services += Start-ManualService -Name Mailpit -FilePath mailpit -ArgumentList @('--listen', '127.0.0.1:8025', '--smtp', '127.0.0.1:1025') -WorkingDirectory (Get-Location).Path -LogDirectory $logDirectory
    }
    Write-Host 'Starting API on http://localhost:5080 ...'
    $services += Start-ManualService -Name API -FilePath dotnet -ArgumentList @('run', '--project', 'src/BranchBooking.Api', '--launch-profile', 'http', '--no-build') -WorkingDirectory (Get-Location).Path -LogDirectory $logDirectory
    Write-Host 'Starting notification Worker ...'
    $services += Start-ManualService -Name Worker -FilePath dotnet -ArgumentList @('run', '--project', 'src/BranchBooking.Worker', '--no-build') -WorkingDirectory (Get-Location).Path -LogDirectory $logDirectory
    Write-Host 'Starting web on http://localhost:4200 ...'
    $npmCommand = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
    $services += Start-ManualService -Name Frontend -FilePath $npmCommand -ArgumentList @('start') -WorkingDirectory $webDirectory -LogDirectory $logDirectory
    while ($true) {
        Assert-ManualServicesRunning -Services $services
        Start-Sleep -Seconds 1
    }
}
finally {
    Stop-ManualServices -Services $services
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
