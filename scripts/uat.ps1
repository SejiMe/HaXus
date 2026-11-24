#requires -Version 7.0
param([ValidateSet('up','down')][string]$Action = 'up')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Environment.ps1')
Import-HaxusEnvironment (Join-Path $repoRoot '.env.uat')
Push-Location $repoRoot
try {
    python scripts/infrastructure.py "uat-$Action"
    if ($LASTEXITCODE -ne 0) { throw 'UAT command failed; no local image fallback is permitted.' }
} finally { Pop-Location }
