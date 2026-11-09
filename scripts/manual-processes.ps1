# Process supervision shared by the manual launcher and its cross-platform tests.
function Assert-ManualPortsAvailable {
    param([Parameter(Mandatory)]$Ports)
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    foreach ($service in $Ports) {
        foreach ($listener in $listeners) {
            if ($listener.Port -eq $service.Port -and
                ([System.Net.IPAddress]::IsLoopback($listener.Address) -or
                 $listener.Address.Equals([System.Net.IPAddress]::Any) -or
                 $listener.Address.Equals([System.Net.IPAddress]::IPv6Any))) {
                throw "$($service.Name) port $($service.Port) is already in use. Stop the existing service or Docker container before running the manual launcher."
            }
        }
    }
}

function Start-ManualService {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$LogDirectory
    )
    $stdout = Join-Path $LogDirectory "$Name.stdout.log"
    $stderr = Join-Path $LogDirectory "$Name.stderr.log"
    # Start-Process joins its ArgumentList; quote tokens containing spaces explicitly.
    $arguments = ($ArgumentList | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ }
    }) -join ' '
    if ($env:OS -eq 'Windows_NT' -and $FilePath -match '\.(cmd|bat)$') {
        # Batch files need cmd.exe when starting with redirected output on Windows.
        $command = (Get-Command $FilePath -CommandType Application -ErrorAction Stop).Source
        $arguments = '/d /s /c ""' + $command + '" ' + $arguments + '"'
        $FilePath = $env:ComSpec
    }
    try {
        $process = Start-Process $FilePath -ArgumentList $arguments -WorkingDirectory $WorkingDirectory `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -NoNewWindow
    }
    catch { throw "Could not start $Name`: $($_.Exception.Message). Logs: $LogDirectory" }
    Write-Host "$Name logs: $stdout and $stderr"
    [PSCustomObject]@{ Name = $Name; Process = $process; Stdout = $stdout; Stderr = $stderr }
}

function Assert-ManualServicesRunning {
    param([Parameter(Mandatory)]$Services)
    foreach ($service in $Services) {
        $service.Process.Refresh()
        if ($service.Process.HasExited) {
            $service.Process.WaitForExit()
            # Keep the original startup output on disk and expose its final lines before the summary.
            foreach ($file in @($service.Stdout, $service.Stderr)) {
                if (Test-Path $file) { Get-Content $file -Tail 20 | Out-Host }
            }
            throw "$($service.Name) exited (code $($service.Process.ExitCode)). Check $($service.Stdout) and $($service.Stderr) for the startup error."
        }
    }
}

function Stop-ManualServices {
    param($Services)
    foreach ($service in $Services) {
        try {
            $process = $service.Process
            if (-not $process.HasExited) {
                if ($env:OS -eq 'Windows_NT') { & taskkill /PID $process.Id /T /F 2>$null | Out-Null }
                else { $process.Kill($true) }
                $process.WaitForExit()
            }
        }
        catch { Write-Warning "Could not stop $($service.Name): $($_.Exception.Message)" }
    }
}
