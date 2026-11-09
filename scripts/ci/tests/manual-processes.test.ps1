$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../manual-processes.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) ('manual-launcher-' + [Guid]::NewGuid().ToString('N'))
$logs = Join-Path $temp 'logs with spaces'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$services = @()
function Assert-True($value, $message) { if (-not $value) { throw $message } }
try {
    # Port checks identify both the service and its port rather than starting a doomed process.
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    try {
        $failure = $null
        try { Assert-ManualPortsAvailable -Ports @(@{ Name = 'API'; Port = $port }) }
        catch { $failure = $_.Exception.Message }
        Assert-True ($failure -like "*API port $port is already in use*") 'Port conflict was not diagnosed'
    }
    finally { $listener.Stop() }
    Assert-ManualPortsAvailable -Ports @(@{ Name = 'API'; Port = $port })
    Write-Host 'PASS occupied and available port checks'

    $fixture = Join-Path $temp 'service with spaces.ps1'
    @'
param([string]$Mode)
[Console]::Out.WriteLine('stdout from test service')
[Console]::Error.WriteLine('intentional startup failure')
if ($Mode -eq 'fail') { exit 17 }
Start-Sleep -Seconds 120
'@ | Set-Content $fixture
    $shell = Join-Path $PSHOME $(if ($env:OS -eq 'Windows_NT') { 'pwsh.exe' } else { 'pwsh' })
    $failed = Start-ManualService -Name API -FilePath $shell -ArgumentList @('-NoProfile', '-File', $fixture, '-Mode', 'fail') -WorkingDirectory $temp -LogDirectory $logs
    $services += $failed
    Assert-True ($failed.Process.WaitForExit(15000)) 'Test service failed to exit'
    $failure = $null
    try { Assert-ManualServicesRunning -Services @($failed) }
    catch { $failure = $_.Exception.Message }
    Assert-True ($failure -like '*API exited (code 17)*') 'Failure omitted service name or exit code'
    Assert-True ($failure.Contains($failed.Stderr)) 'Failure omitted startup log path'
    Assert-True ((Get-Content $failed.Stdout -Raw) -like '*stdout from test service*') 'stdout was not retained'
    Assert-True ((Get-Content $failed.Stderr -Raw) -like '*intentional startup failure*') 'stderr was not retained'
    Write-Host 'PASS named failures, exit code, output retention and paths containing spaces'

    $healthy = Start-ManualService -Name Worker -FilePath $shell -ArgumentList @('-NoProfile', '-File', $fixture, '-Mode', 'wait') -WorkingDirectory $temp -LogDirectory $logs
    $services += $healthy
    Assert-ManualServicesRunning -Services @($healthy)
    Stop-ManualServices -Services @($healthy)
    Assert-True $healthy.Process.HasExited 'Cleanup left the service running'
    Write-Host 'PASS healthy service monitoring and cleanup'

    if ($env:OS -eq 'Windows_NT') {
        $batch = Join-Path $temp 'npm fixture.cmd'
        "@echo off`r`necho batch stdout`r`necho batch stderr 1>&2`r`nexit /b 19`r`n" | Set-Content $batch -Encoding ascii
        $frontend = Start-ManualService -Name Frontend -FilePath $batch -ArgumentList @('start') -WorkingDirectory $temp -LogDirectory $logs
        $services += $frontend
        Assert-True ($frontend.Process.WaitForExit(15000)) 'Batch file failed to exit'
        $failure = $null
        try { Assert-ManualServicesRunning -Services @($frontend) }
        catch { $failure = $_.Exception.Message }
        Assert-True ($failure -like '*Frontend exited (code 19)*') 'Windows batch exit status was lost'
        Assert-True ((Get-Content $frontend.Stdout -Raw) -like '*batch stdout*') 'Windows batch stdout was lost'
        Write-Host 'PASS Windows batch file startup and error capture'
    }
}
finally {
    Stop-ManualServices -Services $services
    Remove-Item $temp -Recurse -Force
}
