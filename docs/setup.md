# Setup

Use .NET SDK 10.0.102, Aspire CLI/bundle 17.0.0, Docker in Linux-container mode, PowerShell 7 and Python 3.10+. Exact package versions are centralized and project lock files are committed.

```powershell
dotnet tool install Aspire.Cli --version 17.0.0 --tool-path .tools
./.tools/aspire.cmd setup
$env:PATH = (Join-Path (Get-Location) '.tools') + ';' + $env:PATH
dotnet restore HaXus.slnx --locked-mode
dotnet build HaXus.slnx -c Release --no-restore
```

AppHost uses DnxPinned to select the matching CLI package. Follow [infrastructure setup](infrastructure.md) for the full local stack, protected bootstrap material, launchers and verification status.

```powershell
if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
pwsh -File scripts/dev.ps1
```

The host now requires an application database connection. A direct launch without `ConnectionStrings:haxus` intentionally fails. Do not bypass required configuration to reproduce the earlier empty skeleton's startup behavior.

For checks without Docker, run `python scripts/verify-startup.py`, `python tests/test_infrastructure_guards.py` and `python scripts/verify-template.py`. Full checks use `pwsh -File scripts/verify.ps1` and require a healthy engine. See the verification evidence for actual results and the remaining Jenkins-image UAT boundary.

When dependencies intentionally change, update central pins, run `dotnet restore HaXus.slnx --force-evaluate`, review lock files, then repeat locked restore and the required checks. Do not change image/package pins silently to resolve a failure.
