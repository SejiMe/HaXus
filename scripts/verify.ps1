#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$logRoot = Join-Path $repoRoot '.artifacts/verification'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$dotnetCommand = (Get-Command dotnet).Source
$originalEnvironment = @{}
foreach ($name in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'ASPNETCORE_URLS', 'PATH')) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

function Assert-PortAvailable([int]$Port) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    try { $listener.Start() }
    catch { throw "Port $Port must be free before verification." }
    finally { $listener.Stop() }
}

function Assert-Status([string]$Path, [int]$Expected) {
    $response = Invoke-WebRequest -Uri "http://localhost:5190$Path" -SkipHttpErrorCheck -TimeoutSec 3
    if ([int]$response.StatusCode -ne $Expected) {
        throw "$Path returned $($response.StatusCode); expected $Expected."
    }
}

function Test-Host([string]$Name, [string]$Project, [string]$Environment, [switch]$AppHost) {
    Assert-PortAvailable 5190
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:DOTNET_ENVIRONMENT = $Environment
    $env:ASPNETCORE_URLS = 'http://localhost:5190'
    $runArguments = @('run', '--project', $Project, '-c', 'Release', '--no-build', '--no-restore')
    if ($AppHost) {
        foreach ($port in @(15888,18889,18890)) { Assert-PortAvailable $port }
        Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
        $runArguments = @('run', '--project', $Project, '--launch-profile', 'http')
    } else {
        $runArguments += '--no-launch-profile'
    }
    $process = Start-Process -FilePath $dotnetCommand -ArgumentList $runArguments -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot "$Name.stdout.log") -RedirectStandardError (Join-Path $logRoot "$Name.stderr.log")
    try {
        $ready = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($process.HasExited) { throw "$Name exited with code $($process.ExitCode); inspect local verification logs." }
            try {
                $expected = if ($Environment -eq 'Development') { 200 } else { 404 }
                Assert-Status '/health' $expected
                $ready = $true
                break
            } catch { Start-Sleep -Milliseconds 300 }
        }
        if (-not $ready) { throw "$Name did not become ready; inspect local verification logs." }
        Assert-Status '/alive' $expected
        Assert-Status '/' 404
        Assert-Status '/weatherforecast' 404
        Write-Output "PASS $Name (health, liveness, and absent sample routes)"
    } finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $process.Dispose()
    }
    $stopDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $stopDeadline) {
        try { Assert-PortAvailable 5190; return }
        catch { Start-Sleep -Milliseconds 200 }
    }
    throw "$Name left port 5190 in use after shutdown."
}

try {
    $env:PATH = (Join-Path $repoRoot '.tools') + [IO.Path]::PathSeparator + $env:PATH
    Test-Host 'development' 'src/HaXus.Ugnay' 'Development'
    Test-Host 'production' 'src/HaXus.Ugnay' 'Production'
    Test-Host 'apphost' 'aspire/HaXus.AppHost' 'Development' -AppHost
    foreach ($port in @(5190,15888,18889,18890)) { Assert-PortAvailable $port }
    Write-Output 'PASS verification process trees stopped and ports released'
} finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process')
    }
}
