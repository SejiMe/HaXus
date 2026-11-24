#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Environment.ps1')
Import-HaxusEnvironment (Join-Path $repoRoot '.env.local')
if (-not $env:HAXUS_ENVIRONMENT) { $env:HAXUS_ENVIRONMENT = 'dev' }
if (-not $env:HAXUS_SECRET_ROOT) { $env:HAXUS_SECRET_ROOT = Join-Path $env:LOCALAPPDATA "HaXus/secrets/$env:HAXUS_ENVIRONMENT" }
$resolvedSecrets = [IO.Path]::GetFullPath($env:HAXUS_SECRET_ROOT)
if ($resolvedSecrets.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $resolvedSecrets -eq $repoRoot) { throw 'Development secrets must be outside the checkout.' }
$env:PATH = (Join-Path $repoRoot '.tools') + [IO.Path]::PathSeparator + $env:PATH
Push-Location $repoRoot
try {
    dotnet build tests/InfrastructureProbes/InfrastructureProbes.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Probe/bootstrap tool build failed.' }
    dotnet tests/InfrastructureProbes/bin/Release/net10.0/InfrastructureProbes.dll prepare $env:HAXUS_SECRET_ROOT
    if ($LASTEXITCODE -ne 0) { throw 'Development bootstrap preparation failed.' }
    dotnet run --project aspire/HaXus.AppHost
    if ($LASTEXITCODE -ne 0) { throw 'Development AppHost stopped with an error; preserve existing data for diagnosis.' }
} finally { Pop-Location }
