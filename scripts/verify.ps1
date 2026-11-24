#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $env:PATH = (Join-Path $repoRoot '.tools') + [IO.Path]::PathSeparator + $env:PATH
    dotnet restore HaXus.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet build HaXus.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    python tests/test_infrastructure_guards.py
    if ($LASTEXITCODE -ne 0) { throw 'Infrastructure guard checks failed.' }
    foreach ($script in @('verify-startup.py', 'verify-template.py', 'infrastructure.py', 'verify-source.py')) {
        if ($script -eq 'infrastructure.py') { python "scripts/$script" verify }
        else { python "scripts/$script" }
        if ($LASTEXITCODE -ne 0) { throw "Verification failed: $script. Inspect the local artifact log; do not claim a complete pass." }
    }
} finally { Pop-Location }
